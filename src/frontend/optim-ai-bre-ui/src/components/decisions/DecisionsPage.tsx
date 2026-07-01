'use client'

import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'

interface Decision {
  id: string
  referenceId: string
  applicantName: string
  applicantPan: string
  loanProduct: string
  loanAmount: number
  decision: 'GREEN' | 'AMBER' | 'RED'
  riskScore: number
  executionTimeMs: number
  ruleSetName: string
  rulesTriggered: number
  deviationsCount: number
  executedAt: string
  executedBy: string
}

const MOCK_DECISIONS: Decision[] = [
  { id: '1', referenceId: 'REF-2024-001821', applicantName: 'Rajesh Kumar Singh', applicantPan: 'ABCDE1234F', loanProduct: 'Vehicle Loan', loanAmount: 750000, decision: 'GREEN', riskScore: 72, executionTimeMs: 142, ruleSetName: 'Vehicle Loan - Origination', rulesTriggered: 12, deviationsCount: 0, executedAt: '2024-06-28 14:32:10', executedBy: 'API Integration' },
  { id: '2', referenceId: 'REF-2024-001820', applicantName: 'Priya Sharma', applicantPan: 'FGHIJ5678K', loanProduct: 'Personal Loan', loanAmount: 200000, decision: 'AMBER', riskScore: 58, executionTimeMs: 98, ruleSetName: 'Personal Loan - Origination', rulesTriggered: 9, deviationsCount: 2, executedAt: '2024-06-28 13:15:44', executedBy: 'Branch Portal' },
  { id: '3', referenceId: 'REF-2024-001819', applicantName: 'Mohammed Farhan', applicantPan: 'KLMNO9012P', loanProduct: 'MSME Loan', loanAmount: 5000000, decision: 'RED', riskScore: 31, executionTimeMs: 201, ruleSetName: 'MSME Loan - Underwriting', rulesTriggered: 18, deviationsCount: 4, executedAt: '2024-06-28 12:08:22', executedBy: 'API Integration' },
  { id: '4', referenceId: 'REF-2024-001818', applicantName: 'Sunita Devi', applicantPan: 'QRSTU3456V', loanProduct: 'Tractor Loan', loanAmount: 1200000, decision: 'GREEN', riskScore: 81, executionTimeMs: 118, ruleSetName: 'Tractor Loan - Origination', rulesTriggered: 10, deviationsCount: 0, executedAt: '2024-06-28 11:45:10', executedBy: 'Mobile App' },
  { id: '5', referenceId: 'REF-2024-001817', applicantName: 'Amit Patel', applicantPan: 'VWXYZ7890A', loanProduct: 'Auto Loan', loanAmount: 900000, decision: 'AMBER', riskScore: 64, executionTimeMs: 155, ruleSetName: 'Auto Loan - Re-KYC', rulesTriggered: 8, deviationsCount: 1, executedAt: '2024-06-28 10:22:55', executedBy: 'Branch Portal' },
  { id: '6', referenceId: 'REF-2024-001816', applicantName: 'Kavitha Reddy', applicantPan: 'BCDEF2345G', loanProduct: 'Vehicle Loan', loanAmount: 350000, decision: 'GREEN', riskScore: 76, executionTimeMs: 132, ruleSetName: 'Vehicle Loan - Origination', rulesTriggered: 12, deviationsCount: 0, executedAt: '2024-06-28 09:55:30', executedBy: 'API Integration' },
  { id: '7', referenceId: 'REF-2024-001815', applicantName: 'Suresh Nair', applicantPan: 'GHIJK6789L', loanProduct: 'CV Loan', loanAmount: 3500000, decision: 'RED', riskScore: 28, executionTimeMs: 178, ruleSetName: 'CV Loan - Origination', rulesTriggered: 14, deviationsCount: 6, executedAt: '2024-06-27 18:30:00', executedBy: 'API Integration' },
  { id: '8', referenceId: 'REF-2024-001814', applicantName: 'Deepa Iyer', applicantPan: 'MNOPQ0123R', loanProduct: 'Personal Loan', loanAmount: 150000, decision: 'GREEN', riskScore: 88, executionTimeMs: 85, ruleSetName: 'Personal Loan - Origination', rulesTriggered: 9, deviationsCount: 0, executedAt: '2024-06-27 17:12:44', executedBy: 'Mobile App' },
]

const DECISION_STYLES: Record<Decision['decision'], { badge: string; dot: string; label: string }> = {
  GREEN:  { badge: 'badge-green',  dot: 'bg-green-500',  label: 'Approved' },
  AMBER:  { badge: 'badge-amber',  dot: 'bg-amber-500',  label: 'Review'   },
  RED:    { badge: 'badge-red',    dot: 'bg-red-500',    label: 'Rejected' },
}

export function DecisionsPage() {
  const [search, setSearch] = useState('')
  const [decisionFilter, setDecisionFilter] = useState('ALL')
  const [productFilter, setProductFilter] = useState('ALL')
  const [expandedId, setExpandedId] = useState<string | null>(null)
  const [dateFrom, setDateFrom] = useState('')

  const { data: decisions = MOCK_DECISIONS, isLoading } = useQuery({
    queryKey: ['decisions'],
    queryFn: async () => {
      try {
        const res = await apiClient.get<Decision[]>('/decisions')
        return res.data
      } catch {
        return MOCK_DECISIONS
      }
    },
  })

  const products = ['ALL', ...Array.from(new Set(decisions.map(d => d.loanProduct)))]

  const filtered = decisions.filter(d => {
    const matchSearch = d.referenceId.toLowerCase().includes(search.toLowerCase()) ||
      d.applicantName.toLowerCase().includes(search.toLowerCase()) ||
      d.applicantPan.toLowerCase().includes(search.toLowerCase())
    const matchDecision = decisionFilter === 'ALL' || d.decision === decisionFilter
    const matchProduct = productFilter === 'ALL' || d.loanProduct === productFilter
    return matchSearch && matchDecision && matchProduct
  })

  const stats = {
    total: decisions.length,
    green: decisions.filter(d => d.decision === 'GREEN').length,
    amber: decisions.filter(d => d.decision === 'AMBER').length,
    red: decisions.filter(d => d.decision === 'RED').length,
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Decisions</h1>
          <p className="text-gray-500 text-sm mt-1">BRE execution history and loan decisions</p>
        </div>
        <button className="btn-secondary">Export CSV</button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-4 gap-4">
        <div className="card text-center">
          <p className="text-3xl font-bold text-gray-900">{stats.total}</p>
          <p className="text-gray-500 text-sm mt-1">Total Decisions</p>
        </div>
        <div className="card text-center">
          <p className="text-3xl font-bold text-green-600">{stats.green}</p>
          <p className="text-gray-500 text-sm mt-1">Approved (GREEN)</p>
        </div>
        <div className="card text-center">
          <p className="text-3xl font-bold text-amber-600">{stats.amber}</p>
          <p className="text-gray-500 text-sm mt-1">Review (AMBER)</p>
        </div>
        <div className="card text-center">
          <p className="text-3xl font-bold text-red-500">{stats.red}</p>
          <p className="text-gray-500 text-sm mt-1">Rejected (RED)</p>
        </div>
      </div>

      {/* Filters */}
      <div className="flex gap-3 flex-wrap">
        <input type="text" placeholder="Search by Ref ID, Name, PAN..." value={search}
          onChange={e => setSearch(e.target.value)} className="input flex-1 min-w-48" />
        <select value={decisionFilter} onChange={e => setDecisionFilter(e.target.value)} className="input w-40">
          <option value="ALL">All Decisions</option>
          <option value="GREEN">GREEN</option>
          <option value="AMBER">AMBER</option>
          <option value="RED">RED</option>
        </select>
        <select value={productFilter} onChange={e => setProductFilter(e.target.value)} className="input w-48">
          {products.map(p => <option key={p} value={p}>{p === 'ALL' ? 'All Products' : p}</option>)}
        </select>
      </div>

      {/* Table */}
      <div className="card p-0 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 border-b border-gray-200">
            <tr>
              {['Reference ID', 'Applicant', 'Product', 'Amount', 'Decision', 'Risk Score', 'Rules', 'Time', 'Executed At'].map(h => (
                <th key={h} className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {isLoading ? (
              <tr><td colSpan={9} className="text-center py-8 text-gray-400">Loading decisions...</td></tr>
            ) : filtered.map(d => (
              <>
                <tr key={d.id} className="hover:bg-gray-50 cursor-pointer transition-colors"
                  onClick={() => setExpandedId(expandedId === d.id ? null : d.id)}>
                  <td className="px-4 py-3 font-mono text-blue-600 text-xs font-medium">{d.referenceId}</td>
                  <td className="px-4 py-3">
                    <p className="font-medium text-gray-900">{d.applicantName}</p>
                    <p className="text-gray-400 text-xs">{d.applicantPan}</p>
                  </td>
                  <td className="px-4 py-3"><span className="badge-blue">{d.loanProduct}</span></td>
                  <td className="px-4 py-3 font-medium text-gray-900">₹{(d.loanAmount / 100000).toFixed(1)}L</td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1.5">
                      <span className={`w-2 h-2 rounded-full ${DECISION_STYLES[d.decision].dot}`} />
                      <span className={DECISION_STYLES[d.decision].badge}>{DECISION_STYLES[d.decision].label}</span>
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <div className="w-16 bg-gray-200 rounded-full h-1.5">
                        <div className={`h-1.5 rounded-full ${d.riskScore >= 70 ? 'bg-green-500' : d.riskScore >= 50 ? 'bg-amber-500' : 'bg-red-500'}`}
                          style={{ width: `${d.riskScore}%` }} />
                      </div>
                      <span className="text-xs font-medium text-gray-700">{d.riskScore}</span>
                    </div>
                  </td>
                  <td className="px-4 py-3 text-gray-600">{d.rulesTriggered} / {d.deviationsCount > 0 ? <span className="text-amber-600">{d.deviationsCount} dev</span> : '0 dev'}</td>
                  <td className="px-4 py-3 text-gray-600">{d.executionTimeMs}ms</td>
                  <td className="px-4 py-3 text-gray-400 text-xs">{d.executedAt}</td>
                </tr>
                {expandedId === d.id && (
                  <tr key={`${d.id}-expand`} className="bg-blue-50">
                    <td colSpan={9} className="px-6 py-4">
                      <div className="grid grid-cols-3 gap-6 text-sm">
                        <div>
                          <p className="font-semibold text-gray-700 mb-2">Execution Details</p>
                          <div className="space-y-1 text-gray-600">
                            <p><span className="text-gray-400">Rule Set:</span> {d.ruleSetName}</p>
                            <p><span className="text-gray-400">Channel:</span> {d.executedBy}</p>
                            <p><span className="text-gray-400">Rules Hit:</span> {d.rulesTriggered}</p>
                            <p><span className="text-gray-400">Deviations:</span> {d.deviationsCount}</p>
                          </div>
                        </div>
                        <div>
                          <p className="font-semibold text-gray-700 mb-2">Applicant</p>
                          <div className="space-y-1 text-gray-600">
                            <p><span className="text-gray-400">Name:</span> {d.applicantName}</p>
                            <p><span className="text-gray-400">PAN:</span> {d.applicantPan}</p>
                            <p><span className="text-gray-400">Product:</span> {d.loanProduct}</p>
                            <p><span className="text-gray-400">Amount:</span> ₹{d.loanAmount.toLocaleString()}</p>
                          </div>
                        </div>
                        <div className="flex items-start gap-2">
                          <button className="btn-primary text-sm">View Full Report</button>
                          <button className="btn-secondary text-sm">Re-execute</button>
                        </div>
                      </div>
                    </td>
                  </tr>
                )}
              </>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
