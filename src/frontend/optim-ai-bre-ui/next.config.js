/** @type {import('next').NextConfig} */
const nextConfig = {
  experimental: {
    optimizePackageImports: ['recharts'],
  },
  images: {
    remotePatterns: [{ protocol: 'https', hostname: '**' }],
  },
}

module.exports = nextConfig
