import axios, { type AxiosInstance, type AxiosError } from 'axios'

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api/v1'

function createApiClient(): AxiosInstance {
  const client = axios.create({
    baseURL: BASE_URL,
    timeout: 30_000,
    headers: { 'Content-Type': 'application/json' },
  })

  // Attach JWT token
  client.interceptors.request.use((config) => {
    const token = typeof window !== 'undefined' ? localStorage.getItem('bre_access_token') : null
    if (token) config.headers.Authorization = `Bearer ${token}`
    return config
  })

  // Handle 401 → refresh or redirect
  client.interceptors.response.use(
    (res) => res,
    async (error: AxiosError) => {
      if (error.response?.status === 401) {
        const refreshToken = localStorage.getItem('bre_refresh_token')
        if (refreshToken) {
          try {
            const { data } = await axios.post(`${BASE_URL}/auth/refresh`, { refreshToken })
            localStorage.setItem('bre_access_token', data.accessToken)
            error.config!.headers!.Authorization = `Bearer ${data.accessToken}`
            return client.request(error.config!)
          } catch {
            localStorage.clear()
            window.location.href = '/login'
          }
        }
      }
      return Promise.reject(error)
    }
  )

  return client
}

const instance = createApiClient()

export const apiClient = {
  get: <T>(url: string, params?: object): Promise<T> =>
    instance.get(url, { params }).then((r) => r.data),

  post: <T>(url: string, data?: object): Promise<T> =>
    instance.post(url, data).then((r) => r.data),

  put: <T>(url: string, data?: object): Promise<T> =>
    instance.put(url, data).then((r) => r.data),

  patch: <T>(url: string, data?: object): Promise<T> =>
    instance.patch(url, data).then((r) => r.data),

  delete: <T>(url: string): Promise<T> =>
    instance.delete(url).then((r) => r.data),
}
