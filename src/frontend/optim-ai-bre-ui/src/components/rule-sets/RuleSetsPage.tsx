'use client'

import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'

interface RuleSet {
  id: string
  name: string
  description: string
  loanProduct: string
  loanStage: string
  status: 'ACTIVE' | 'INACTIVE' | 'DRAFT'
  ruleCount: number
  version: number
  executionCount: number
  avgDecisionMs: number
  approvalRate: number
  createdAt: string
  updatedAt: string
}

const MOCK_RULE_SETS: RuleSet[] = [
  { id: '1', name: 'Vehicle Loan - Origination', description: 'Complete rule set for vehicle loan origination decisioning', loanProduct: 'Vehicle Loan', loanStage: 'Origination', status: 'ACTIVE', ruleCount: 12, version: 5, executionCount: 18420, avgDecisionMs: 142, approvalRate: 68.4, createdAt: '2024-01-10', updatedAt: '2024-06-01' },
  { id: '2', name: 'Tractor Loan - Origination', description: 'Rule set for tractor and agri equipment loans', loanProduct: 'Tractor Loan', loanStage: 'Origination', status: 'ACTIVE', ruleCount: 10, version: 3, executionCount: 8241, avgDecisionMs: 118, approvalRate: 72.1, createdAt: '2024-02-01', updatedAt: '2024-05-15' },
  { id: '3', name: 'MSME Loan - Underwriting', description: 'MSME underwriting rules with GST and ITR checks', loanProduct: 'MSME Loan', loanStage: 'Underwriting', status: 'ACTIVE', ruleCount: 18, version: 7, executionCount: 5184, avgDecisionMs: 198, approvalRate: 58.2, createdAt: '2024-01-15', updatedAt: '2024-06-10' },
  { id: '4', name: 'Auto Loan - Re-KYC', description: 'Re-KYC rules for auto loan renewals', loanProduct: 'Auto Loan', loanStage: 'Re-KYC', status: 'ACTIVE', ruleCount: 8, version: 2, executionCount: 3210, avgDecisionMs: 95, approvalRate: 81.5, createdAt: '2024-03-01', updatedAt: '2024-04-20' },
  { id: '5', name: 'CV Loan - Origination', description: 'Commercial vehicle loan origination rules', loanProduct: 'CV Loan', loanStage: 'Origination', status: 'DRAFT', ruleCount: 14, version: 1, executionCount: 0, avgDecisionMs: 0, approvalRate: 0, createdAt: '2024-06-15', updatedAt: '2024-06-15' },
  { id: '6', name: 'Personal Loan - Origination', description: 'Personal loan quick decisioning rules', loanProduct: 'Personal Loan', loanStage: 'Origination', status: 'INACTIVE', ruleCount: 9, version: 4, executionCount: 11024, avgDecisionMs: 88, approvalRate: 54.8, createdAt: '2024-01-10', updatedAt: '2024-05-01' },
]

const STAGE_COLORS: Record<string, string> = {
  'Origination': 'badge-blue',
  'Underwriting': 'badge-purple',
  'Re-KYC': 'badge-amber',
  'Pre-Sanction': 'badge-green',
  'Post-Disbursement': 'badge-red',
}

export function RuleSetsPage() {
  const [selected, setSelected] = useState<RuleSet | null>(null)
  const [search, setSearch] = useState('')
  const [productFilter, setProductFilter] = useState('ALL')

  const { data: ruleSets = MOCK_RULE_SETS, isLoading } = useQuery({
    queryKey: ['rule-sets'],
    queryFn: async () => {
      try {
        const res = await apiClient.get<RuleSet[]>('/rule-sets')
        return res.data
      } catch {
        return MOCK_RULE_SETS
      }
    },
  })

  const products = ['ALL', ...Array.from(new Set(ruleSets.map(r => r.loanProduct)))]

  const filtered = ruleSets.filter(rs => {
    const matchSearch = rs.name.toLowerCase().includes(search.toLowerCase()) ||
      rs.loanProduct.toLowerCase().includes(search.toLowerCase())
    const matchProduct = productFilter === 'ALL' || rs.loanProduct === productFilter
    return matchSearch && matchProduct
  })

  const active = ruleSets.filter(r => r.status === 'ACTIVE')
  const totalExec = ruleSets.reduce((s, r) => s + r.executionCount, 0)
  const avgApproval = active.length ? (active.reduce((s, r) => s + r.approvalRate, 0) / active.length).toFixed(1) : '—'

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Rule Sets</h1>
          <p className="text-gray-500 text-sm mt-1">Manage rule sets by loan product and stage</p>
        </div>
        <button className="btn-primary">+ New Rule Set</button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-4 gap-4">
        {[
          { label: 'Total Rule Sets', value: ruleSets.length },
          { label: 'Active', value: active.length },
          { label: 'Total Executions', value: totalExec.toLocaleString() },
          { label: 'Avg Approval Rate', value: `${avgApproval}%` },
        ].map(s => (
          <div key={s.label} className="card text-center">
            <p className="text-3xl font-bold text-gray-900">{s.value}</p>
            <p className="text-gray-500 text-sm mt-1">{s.label}</p>
          </div>
        ))}
      </div>

      {/* Filters */}
      <div className="flex gap-3">
        <input
          type="text"
          placeholder="Search rule sets..."
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="input flex-1"
        />
        <select value={productFilter} onChange={e => setProductFilter(e.target.value)} className="input w-48">
          {products.map(p => <option key={p} value={p}>{p === 'ALL' ? 'All Products' : p}</option>)}
        </select>
      </div>

      {/* Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-2 xl:grid-cols-3 gap-4">
        {isLoading ? (
          <p className="col-span-3 text-center py-12 text-gray-400">Loading rule sets...</p>
        ) : filtered.map(rs => (
          <div key={rs.id} className={`card cursor-pointer transition-all hover:shadow-md ${selected?.id === rs.id ? 'ring-2 ring-blue-500' : ''}`}
            onClick={() => setSelected(selected?.id === rs.id ? null : rs)}>
            <div className="flex items-start justify-between mb-3">
              <div>
                <h3 className="font-semibold text-gray-900">{rs.name}</h3>
                <p className="text-gray-400 text-xs mt-0.5">{rs.description}</p>
              </div>
              <span className={rs.status === 'ACTIVE' ? 'badge-green' : rs.status === 'DRAFT' ? 'badge-amber' : 'badge-red'}>
                {rs.status}
              </span>
            </div>

            <div className="flex gap-2 mb-4">
              <span className="badge-blue">{rs.loanProduct}</span>
              <span className={STAGE_COLORS[rs.loanStage] || 'badge-blue'}>{rs.loanStage}</span>
            </div>

            <div className="grid grid-cols-2 gap-3 text-sm">
              <div className="bg-gray-50 rounded-lg p-2 text-center">
                <p className="font-bold text-gray-900">{rs.ruleCount}</p>
                <p className="text-gray-400 text-xs">Rules</p>
              </div>
              <div className="bg-gray-50 rounded-lg p-2 text-center">
                <p className="font-bold text-gray-900">v{rs.version}</p>
                <p className="text-gray-400 text-xs">Version</p>
              </div>
              <div className="bg-gray-50 rounded-lg p-2 text-center">
                <p className="font-bold text-gray-900">{rs.executionCount > 0 ? rs.executionCount.toLocaleString() : '—'}</p>
                <p className="text-gray-400 text-xs">Executions</p>
              </div>
              <div className="bg-gray-50 rounded-lg p-2 text-center">
                <p className={`font-bold ${rs.approvalRate > 70 ? 'text-green-600' : rs.approvalRate > 50 ? 'text-amber-600' : 'text-red-500'}`}>
                  {rs.approvalRate > 0 ? `${rs.approvalRate}%` : '—'}
                </p>
                <p className="text-gray-400 text-xs">Approval</p>
              </div>
            </div>

            {selected?.id === rs.id && (
              <div className="mt-4 pt-4 border-t border-gray-100 flex gap-2">
                <button className="btn-primary flex-1 text-sm">Edit Rule Set</button>
                <button className="btn-secondary flex-1 text-sm">View Executions</button>
                <button className="btn-secondary text-sm">Clone</button>
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}
