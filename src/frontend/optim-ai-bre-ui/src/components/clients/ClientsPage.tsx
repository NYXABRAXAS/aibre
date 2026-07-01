'use client'

import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'

interface Client {
  id: string
  name: string
  code: string
  type: 'BANK' | 'NBFC' | 'MFI' | 'FINTECH'
  contactName: string
  contactEmail: string
  contactPhone: string
  status: 'ACTIVE' | 'INACTIVE' | 'SUSPENDED'
  apiKey: string
  executionsToday: number
  executionsTotal: number
  ruleSetCount: number
  createdAt: string
  lastActivity: string
}

const MOCK_CLIENTS: Client[] = [
  { id: '1', name: 'Demo Bank Limited', code: 'DEMO_BANK', type: 'BANK', contactName: 'Rahul Verma', contactEmail: 'rahul.verma@demobank.in', contactPhone: '+91 98765 43210', status: 'ACTIVE', apiKey: 'brk_live_demo_****', executionsToday: 1284, executionsTotal: 48201, ruleSetCount: 6, createdAt: '2024-01-10', lastActivity: '2024-06-28 14:30' },
  { id: '2', name: 'QuickFinance NBFC', code: 'QUICK_FIN', type: 'NBFC', contactName: 'Meena Iyer', contactEmail: 'meena@quickfinance.com', contactPhone: '+91 87654 32109', status: 'ACTIVE', apiKey: 'brk_live_qfin_****', executionsToday: 842, executionsTotal: 21050, ruleSetCount: 4, createdAt: '2024-02-15', lastActivity: '2024-06-28 13:45' },
  { id: '3', name: 'AgriCredit Solutions', code: 'AGRI_CRED', type: 'NBFC', contactName: 'Suresh Patil', contactEmail: 'suresh@agricredit.in', contactPhone: '+91 76543 21098', status: 'ACTIVE', apiKey: 'brk_live_agri_****', executionsToday: 310, executionsTotal: 8420, ruleSetCount: 3, createdAt: '2024-03-01', lastActivity: '2024-06-28 11:20' },
  { id: '4', name: 'MicroFin Trust', code: 'MICRO_FIN', type: 'MFI', contactName: 'Asha Sharma', contactEmail: 'asha@microfin.org', contactPhone: '+91 65432 10987', status: 'INACTIVE', apiKey: 'brk_test_mfin_****', executionsToday: 0, executionsTotal: 3210, ruleSetCount: 2, createdAt: '2024-04-10', lastActivity: '2024-05-30 16:00' },
  { id: '5', name: 'SwiftLoan Technologies', code: 'SWIFT_LN', type: 'FINTECH', contactName: 'Vikram Malhotra', contactEmail: 'vikram@swiftloan.tech', contactPhone: '+91 54321 09876', status: 'ACTIVE', apiKey: 'brk_live_swift_****', executionsToday: 2140, executionsTotal: 92400, ruleSetCount: 8, createdAt: '2024-01-20', lastActivity: '2024-06-28 14:55' },
]

const TYPE_BADGE: Record<Client['type'], string> = {
  BANK: 'badge-blue',
  NBFC: 'badge-purple',
  MFI: 'badge-amber',
  FINTECH: 'badge-green',
}

export function ClientsPage() {
  const [search, setSearch] = useState('')
  const [typeFilter, setTypeFilter] = useState('ALL')
  const [selectedClient, setSelectedClient] = useState<Client | null>(null)
  const [showModal, setShowModal] = useState(false)

  const { data: clients = MOCK_CLIENTS, isLoading } = useQuery({
    queryKey: ['clients'],
    queryFn: async () => {
      try {
        return await apiClient.get<Client[]>('/clients')
      } catch {
        return MOCK_CLIENTS
      }
    },
  })

  const filtered = clients.filter(c => {
    const matchSearch = c.name.toLowerCase().includes(search.toLowerCase()) ||
      c.code.toLowerCase().includes(search.toLowerCase()) ||
      c.contactEmail.toLowerCase().includes(search.toLowerCase())
    const matchType = typeFilter === 'ALL' || c.type === typeFilter
    return matchSearch && matchType
  })

  const stats = {
    total: clients.length,
    active: clients.filter(c => c.status === 'ACTIVE').length,
    todayExec: clients.reduce((s, c) => s + c.executionsToday, 0),
    totalExec: clients.reduce((s, c) => s + c.executionsTotal, 0),
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Clients</h1>
          <p className="text-gray-500 text-sm mt-1">Manage tenants and API access for banks and NBFCs</p>
        </div>
        <button onClick={() => setShowModal(true)} className="btn-primary">+ Add Client</button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-4 gap-4">
        {[
          { label: 'Total Clients', value: stats.total },
          { label: 'Active', value: stats.active },
          { label: "Today's Executions", value: stats.todayExec.toLocaleString() },
          { label: 'All-time Executions', value: stats.totalExec.toLocaleString() },
        ].map(s => (
          <div key={s.label} className="card text-center">
            <p className="text-3xl font-bold text-gray-900">{s.value}</p>
            <p className="text-gray-500 text-sm mt-1">{s.label}</p>
          </div>
        ))}
      </div>

      {/* Filters */}
      <div className="flex gap-3">
        <input type="text" placeholder="Search clients..."
          value={search} onChange={e => setSearch(e.target.value)} className="input flex-1" />
        <select value={typeFilter} onChange={e => setTypeFilter(e.target.value)} className="input w-40">
          <option value="ALL">All Types</option>
          <option value="BANK">Bank</option>
          <option value="NBFC">NBFC</option>
          <option value="MFI">MFI</option>
          <option value="FINTECH">Fintech</option>
        </select>
      </div>

      {/* Table */}
      <div className="card p-0 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 border-b border-gray-200">
            <tr>
              {['Client', 'Type', 'Status', 'Rule Sets', "Today's Usage", 'Total Executions', 'Last Activity', 'Actions'].map(h => (
                <th key={h} className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {isLoading ? (
              <tr><td colSpan={8} className="text-center py-8 text-gray-400">Loading clients...</td></tr>
            ) : filtered.map(client => (
              <tr key={client.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <p className="font-medium text-gray-900">{client.name}</p>
                  <p className="text-gray-400 text-xs font-mono">{client.code}</p>
                </td>
                <td className="px-4 py-3"><span className={TYPE_BADGE[client.type]}>{client.type}</span></td>
                <td className="px-4 py-3">
                  <span className={client.status === 'ACTIVE' ? 'badge-green' : client.status === 'SUSPENDED' ? 'badge-red' : 'badge-amber'}>
                    {client.status}
                  </span>
                </td>
                <td className="px-4 py-3 text-gray-900 font-medium text-center">{client.ruleSetCount}</td>
                <td className="px-4 py-3 text-gray-900 font-medium">{client.executionsToday.toLocaleString()}</td>
                <td className="px-4 py-3 text-gray-600">{client.executionsTotal.toLocaleString()}</td>
                <td className="px-4 py-3 text-gray-400 text-xs">{client.lastActivity}</td>
                <td className="px-4 py-3">
                  <div className="flex gap-2">
                    <button onClick={() => setSelectedClient(client)} className="text-blue-600 hover:text-blue-800 text-xs font-medium">View</button>
                    <button className="text-gray-400 hover:text-gray-600 text-xs font-medium">Edit</button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Client Detail Modal */}
      {selectedClient && (
        <div className="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center p-4" onClick={() => setSelectedClient(null)}>
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-lg" onClick={e => e.stopPropagation()}>
            <div className="p-6">
              <div className="flex items-center justify-between mb-4">
                <h2 className="text-lg font-bold text-gray-900">{selectedClient.name}</h2>
                <button onClick={() => setSelectedClient(null)} className="text-gray-400 hover:text-gray-600 text-xl">×</button>
              </div>
              <div className="space-y-3 text-sm">
                <div className="grid grid-cols-2 gap-3">
                  {[
                    { label: 'Code', value: selectedClient.code },
                    { label: 'Type', value: selectedClient.type },
                    { label: 'Status', value: selectedClient.status },
                    { label: 'Rule Sets', value: selectedClient.ruleSetCount },
                  ].map(f => (
                    <div key={f.label} className="bg-gray-50 rounded-lg p-3">
                      <p className="text-gray-400 text-xs">{f.label}</p>
                      <p className="font-medium text-gray-900 mt-0.5">{f.value}</p>
                    </div>
                  ))}
                </div>
                <div className="bg-gray-50 rounded-lg p-3">
                  <p className="text-gray-400 text-xs mb-1">Contact</p>
                  <p className="font-medium text-gray-900">{selectedClient.contactName}</p>
                  <p className="text-gray-500 text-xs">{selectedClient.contactEmail}</p>
                  <p className="text-gray-500 text-xs">{selectedClient.contactPhone}</p>
                </div>
                <div className="bg-gray-900 rounded-lg p-3">
                  <p className="text-gray-400 text-xs mb-1">API Key</p>
                  <p className="font-mono text-green-400 text-xs">{selectedClient.apiKey}</p>
                </div>
              </div>
              <div className="flex gap-2 mt-4">
                <button className="btn-primary flex-1">Edit Client</button>
                <button className="btn-secondary flex-1">Regenerate API Key</button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
