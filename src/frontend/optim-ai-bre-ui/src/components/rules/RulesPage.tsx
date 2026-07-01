'use client'

import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import { RuleBuilder } from '@/components/rule-builder/RuleBuilder'

interface Rule {
  id: string
  name: string
  description: string
  category: string
  status: 'DRAFT' | 'PUBLISHED' | 'ARCHIVED'
  priority: number
  version: number
  hitCount: number
  createdAt: string
  updatedAt: string
}

const MOCK_RULES: Rule[] = [
  { id: '1', name: 'CIBIL Score Check', description: 'Reject if CIBIL score < 650', category: 'Credit Bureau', status: 'PUBLISHED', priority: 1, version: 3, hitCount: 4821, createdAt: '2024-01-10', updatedAt: '2024-06-15' },
  { id: '2', name: 'Age Eligibility', description: 'Applicant must be 21–65 years', category: 'KYC', status: 'PUBLISHED', priority: 2, version: 2, hitCount: 5102, createdAt: '2024-01-10', updatedAt: '2024-05-20' },
  { id: '3', name: 'FOIR Calculation', description: 'FOIR must not exceed 50%', category: 'Income', status: 'PUBLISHED', priority: 3, version: 4, hitCount: 3944, createdAt: '2024-01-15', updatedAt: '2024-06-01' },
  { id: '4', name: 'DPD 24M Check', description: 'No DPD > 30 in last 24 months', category: 'Credit Bureau', status: 'PUBLISHED', priority: 4, version: 1, hitCount: 2187, createdAt: '2024-02-01', updatedAt: '2024-02-01' },
  { id: '5', name: 'Employer Stability', description: 'Minimum 12 months vintage at current employer', category: 'Employment', status: 'DRAFT', priority: 5, version: 1, hitCount: 0, createdAt: '2024-06-20', updatedAt: '2024-06-20' },
  { id: '6', name: 'Loan-to-Value Ratio', description: 'LTV must not exceed 85% for two-wheelers', category: 'Collateral', status: 'PUBLISHED', priority: 6, version: 2, hitCount: 1854, createdAt: '2024-03-01', updatedAt: '2024-05-10' },
  { id: '7', name: 'Wilful Defaulter Block', description: 'Hard reject for wilful defaulters', category: 'Compliance', status: 'PUBLISHED', priority: 7, version: 1, hitCount: 312, createdAt: '2024-01-10', updatedAt: '2024-01-10' },
  { id: '8', name: 'Written-Off Amount', description: 'Reject if write-off > ₹25,000', category: 'Credit Bureau', status: 'ARCHIVED', priority: 8, version: 2, hitCount: 489, createdAt: '2024-01-10', updatedAt: '2024-04-01' },
]

const STATUS_BADGE: Record<Rule['status'], string> = {
  PUBLISHED: 'badge-green',
  DRAFT: 'badge-amber',
  ARCHIVED: 'badge-red',
}

export function RulesPage() {
  const [view, setView] = useState<'list' | 'builder'>('list')
  const [editingRule, setEditingRule] = useState<Rule | null>(null)
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('ALL')
  const qc = useQueryClient()

  const { data: rules = MOCK_RULES, isLoading } = useQuery({
    queryKey: ['rules'],
    queryFn: async () => {
      try {
        const res = await apiClient.get<Rule[]>('/rules')
        return res.data
      } catch {
        return MOCK_RULES
      }
    },
  })

  const filtered = rules.filter(r => {
    const matchSearch = r.name.toLowerCase().includes(search.toLowerCase()) ||
      r.description.toLowerCase().includes(search.toLowerCase()) ||
      r.category.toLowerCase().includes(search.toLowerCase())
    const matchStatus = statusFilter === 'ALL' || r.status === statusFilter
    return matchSearch && matchStatus
  })

  const stats = {
    total: rules.length,
    published: rules.filter(r => r.status === 'PUBLISHED').length,
    draft: rules.filter(r => r.status === 'DRAFT').length,
    archived: rules.filter(r => r.status === 'ARCHIVED').length,
  }

  if (view === 'builder') {
    return (
      <div className="p-6">
        <div className="flex items-center gap-3 mb-6">
          <button onClick={() => { setView('list'); setEditingRule(null) }} className="btn-secondary text-sm">
            ← Back to Rules
          </button>
          <h1 className="text-xl font-bold text-gray-900">
            {editingRule ? `Edit: ${editingRule.name}` : 'New Rule'}
          </h1>
        </div>
        <RuleBuilder />
      </div>
    )
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Rule Designer</h1>
          <p className="text-gray-500 text-sm mt-1">Create and manage business rules for loan decisioning</p>
        </div>
        <button onClick={() => setView('builder')} className="btn-primary">
          + New Rule
        </button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-4 gap-4">
        {[
          { label: 'Total Rules', value: stats.total, color: 'text-gray-900' },
          { label: 'Published', value: stats.published, color: 'text-green-600' },
          { label: 'Draft', value: stats.draft, color: 'text-amber-600' },
          { label: 'Archived', value: stats.archived, color: 'text-red-500' },
        ].map(s => (
          <div key={s.label} className="card text-center">
            <p className={`text-3xl font-bold ${s.color}`}>{s.value}</p>
            <p className="text-gray-500 text-sm mt-1">{s.label}</p>
          </div>
        ))}
      </div>

      {/* Filters */}
      <div className="flex gap-3">
        <input
          type="text"
          placeholder="Search rules..."
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="input flex-1"
        />
        <select value={statusFilter} onChange={e => setStatusFilter(e.target.value)} className="input w-40">
          <option value="ALL">All Status</option>
          <option value="PUBLISHED">Published</option>
          <option value="DRAFT">Draft</option>
          <option value="ARCHIVED">Archived</option>
        </select>
      </div>

      {/* Rules Table */}
      <div className="card p-0 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 border-b border-gray-200">
            <tr>
              {['Priority', 'Rule Name', 'Category', 'Status', 'Version', 'Hit Count', 'Updated', 'Actions'].map(h => (
                <th key={h} className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {isLoading ? (
              <tr><td colSpan={8} className="text-center py-8 text-gray-400">Loading rules...</td></tr>
            ) : filtered.length === 0 ? (
              <tr><td colSpan={8} className="text-center py-8 text-gray-400">No rules found</td></tr>
            ) : filtered.map(rule => (
              <tr key={rule.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3 text-center">
                  <span className="inline-flex items-center justify-center w-7 h-7 rounded-full bg-blue-100 text-blue-700 font-bold text-xs">{rule.priority}</span>
                </td>
                <td className="px-4 py-3">
                  <p className="font-medium text-gray-900">{rule.name}</p>
                  <p className="text-gray-400 text-xs mt-0.5">{rule.description}</p>
                </td>
                <td className="px-4 py-3">
                  <span className="badge-blue">{rule.category}</span>
                </td>
                <td className="px-4 py-3">
                  <span className={STATUS_BADGE[rule.status]}>{rule.status}</span>
                </td>
                <td className="px-4 py-3 text-gray-600">v{rule.version}</td>
                <td className="px-4 py-3 text-gray-900 font-medium">{rule.hitCount.toLocaleString()}</td>
                <td className="px-4 py-3 text-gray-400 text-xs">{rule.updatedAt}</td>
                <td className="px-4 py-3">
                  <div className="flex gap-2">
                    <button onClick={() => { setEditingRule(rule); setView('builder') }} className="text-blue-600 hover:text-blue-800 text-xs font-medium">Edit</button>
                    <button className="text-gray-400 hover:text-gray-600 text-xs font-medium">Clone</button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
