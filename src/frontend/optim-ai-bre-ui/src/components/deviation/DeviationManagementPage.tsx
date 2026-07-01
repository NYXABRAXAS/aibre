'use client'

import React, { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import type { PagedResponse } from '@/types'
import { apiClient } from '@/lib/api-client'

interface DeviationRecord {
  id: string
  requestId: string
  applicationId?: string
  deviationCode: string
  deviationName: string
  severity: 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL'
  reason: string
  fieldPath?: string
  actualValue?: string
  expectedValue?: string
  recommendedAction?: string
  isOverridden: boolean
  overrideReason?: string
  createdAt: string
  ruleCode?: string
}

interface DeviationType {
  id: string
  deviationCode: string
  deviationName: string
  category: string
  defaultSeverity: string
  description: string
  recommendedAction: string
  requiresApproval: boolean
}

const SEVERITY_CONFIG = {
  LOW: { bg: 'bg-blue-50', text: 'text-blue-700', border: 'border-blue-200', dot: 'bg-blue-500' },
  MEDIUM: { bg: 'bg-yellow-50', text: 'text-yellow-700', border: 'border-yellow-200', dot: 'bg-yellow-500' },
  HIGH: { bg: 'bg-orange-50', text: 'text-orange-700', border: 'border-orange-200', dot: 'bg-orange-500' },
  CRITICAL: { bg: 'bg-red-50', text: 'text-red-700', border: 'border-red-200', dot: 'bg-red-500' },
}

export function DeviationManagementPage() {
  const qc = useQueryClient()
  const [activeTab, setActiveTab] = useState<'live' | 'types'>('live')
  const [selectedSeverity, setSelectedSeverity] = useState<string>('')
  const [search, setSearch] = useState('')
  const [overrideModal, setOverrideModal] = useState<DeviationRecord | null>(null)
  const [overrideReason, setOverrideReason] = useState('')

  const { data: deviations } = useQuery<PagedResponse<DeviationRecord>>({
    queryKey: ['deviations', selectedSeverity, search],
    queryFn: () => apiClient.get('/deviations', { severity: selectedSeverity, search }),
    refetchInterval: 30_000,
  })

  const { data: deviationTypes } = useQuery<PagedResponse<DeviationType>>({
    queryKey: ['deviation-types'],
    queryFn: () => apiClient.get('/deviation-types'),
  })

  const overrideMutation = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) =>
      apiClient.post(`/deviations/${id}/override`, { reason }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['deviations'] })
      setOverrideModal(null)
      setOverrideReason('')
    },
  })

  const stats = {
    total: deviations?.totalCount ?? 0,
    critical: deviations?.items.filter((d) => d.severity === 'CRITICAL').length ?? 0,
    overridden: deviations?.items.filter((d) => d.isOverridden).length ?? 0,
    pending: deviations?.items.filter((d) => !d.isOverridden).length ?? 0,
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-gray-900">Deviation Management</h1>
          <p className="text-sm text-gray-500 mt-0.5">Track, analyze, and override policy deviations</p>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-4 gap-4">
        {[
          { label: 'Total Deviations', value: stats.total, icon: '📋', color: 'blue' },
          { label: 'Critical', value: stats.critical, icon: '🚨', color: 'red' },
          { label: 'Pending Override', value: stats.pending, icon: '⏳', color: 'orange' },
          { label: 'Overridden', value: stats.overridden, icon: '✅', color: 'emerald' },
        ].map(({ label, value, icon, color }) => (
          <div key={label} className="bg-white rounded-xl border border-gray-200 p-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-gray-500">{label}</p>
                <p className="text-2xl font-bold text-gray-900 mt-1">{value}</p>
              </div>
              <div className="text-2xl">{icon}</div>
            </div>
          </div>
        ))}
      </div>

      {/* Tabs */}
      <div className="flex border-b border-gray-200">
        {[
          { id: 'live', label: 'Live Deviations' },
          { id: 'types', label: 'Deviation Types' },
        ].map(({ id, label }) => (
          <button
            key={id}
            onClick={() => setActiveTab(id as any)}
            className={`py-3 px-6 text-sm font-medium border-b-2 transition-colors ${
              activeTab === id ? 'border-blue-600 text-blue-600' : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {activeTab === 'live' && (
        <div className="bg-white rounded-xl border border-gray-200">
          {/* Filters */}
          <div className="px-5 py-4 border-b border-gray-100 flex items-center gap-3">
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search deviations..."
              className="flex-1 text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            <select
              value={selectedSeverity}
              onChange={(e) => setSelectedSeverity(e.target.value)}
              className="text-sm border border-gray-200 rounded-lg px-3 py-2"
            >
              <option value="">All Severities</option>
              {['LOW', 'MEDIUM', 'HIGH', 'CRITICAL'].map((s) => (
                <option key={s} value={s}>{s}</option>
              ))}
            </select>
          </div>

          {/* Table */}
          <div className="overflow-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs font-semibold text-gray-500 uppercase border-b border-gray-100">
                  <th className="px-5 py-3">Deviation</th>
                  <th className="px-5 py-3">Severity</th>
                  <th className="px-5 py-3">Application</th>
                  <th className="px-5 py-3">Actual Value</th>
                  <th className="px-5 py-3">Reason</th>
                  <th className="px-5 py-3">Status</th>
                  <th className="px-5 py-3">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {deviations?.items.map((d) => {
                  const sc = SEVERITY_CONFIG[d.severity]
                  return (
                    <tr key={d.id} className={`hover:bg-gray-50 ${d.severity === 'CRITICAL' ? 'bg-red-50/30' : ''}`}>
                      <td className="px-5 py-3">
                        <div className="font-medium text-gray-800">{d.deviationName}</div>
                        <div className="text-xs text-gray-400 font-mono">{d.deviationCode}</div>
                      </td>
                      <td className="px-5 py-3">
                        <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold ${sc.bg} ${sc.text} border ${sc.border}`}>
                          <span className={`w-1.5 h-1.5 rounded-full ${sc.dot}`} />
                          {d.severity}
                        </span>
                      </td>
                      <td className="px-5 py-3 font-mono text-xs text-gray-600">{d.applicationId ?? '—'}</td>
                      <td className="px-5 py-3">
                        {d.fieldPath && (
                          <div>
                            <div className="text-xs text-gray-400">{d.fieldPath}</div>
                            <div className="font-mono text-red-600 font-medium">{d.actualValue ?? '—'}</div>
                          </div>
                        )}
                      </td>
                      <td className="px-5 py-3 text-xs text-gray-600 max-w-48 truncate">{d.reason}</td>
                      <td className="px-5 py-3">
                        {d.isOverridden ? (
                          <span className="text-xs bg-emerald-100 text-emerald-700 px-2 py-1 rounded-full font-medium">Overridden</span>
                        ) : (
                          <span className="text-xs bg-amber-100 text-amber-700 px-2 py-1 rounded-full font-medium">Pending</span>
                        )}
                      </td>
                      <td className="px-5 py-3">
                        {!d.isOverridden && (
                          <button
                            onClick={() => setOverrideModal(d)}
                            className="text-xs text-blue-600 hover:text-blue-700 font-medium"
                          >
                            Override
                          </button>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {activeTab === 'types' && (
        <div className="grid grid-cols-2 gap-4">
          {deviationTypes?.items.map((dt) => {
            const sc = SEVERITY_CONFIG[dt.defaultSeverity as keyof typeof SEVERITY_CONFIG] ?? SEVERITY_CONFIG.MEDIUM
            return (
              <div key={dt.id} className={`rounded-xl border-2 p-4 ${sc.border} ${sc.bg}`}>
                <div className="flex items-center justify-between mb-2">
                  <div className="font-semibold text-gray-800">{dt.deviationName}</div>
                  <span className={`text-xs px-2 py-0.5 rounded-full font-semibold ${sc.text} bg-white`}>
                    {dt.defaultSeverity}
                  </span>
                </div>
                <div className="text-xs font-mono text-gray-500 mb-2">{dt.deviationCode}</div>
                <div className="text-sm text-gray-600 mb-3">{dt.description}</div>
                {dt.recommendedAction && (
                  <div className="flex items-start gap-1.5 text-xs text-gray-500">
                    <span>💡</span>
                    <span>{dt.recommendedAction}</span>
                  </div>
                )}
                {dt.requiresApproval && (
                  <div className="mt-2 text-xs text-amber-600 font-medium">⚠ Requires approval</div>
                )}
              </div>
            )
          })}
        </div>
      )}

      {/* Override Modal */}
      {overrideModal && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md">
            <div className="px-6 py-5 border-b border-gray-100">
              <h2 className="text-lg font-bold text-gray-900">Override Deviation</h2>
              <p className="text-sm text-gray-500 mt-1">{overrideModal.deviationName}</p>
            </div>
            <div className="px-6 py-5 space-y-4">
              <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 text-sm text-amber-800">
                <p className="font-semibold mb-1">⚠ Override Reason Required</p>
                <p>You must provide a valid business justification for overriding this deviation. This action will be logged in the audit trail.</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-700 block mb-1">Business Justification</label>
                <textarea
                  value={overrideReason}
                  onChange={(e) => setOverrideReason(e.target.value)}
                  rows={4}
                  placeholder="Enter detailed reason for overriding this deviation..."
                  className="w-full text-sm border border-gray-200 rounded-xl px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
                />
              </div>
            </div>
            <div className="px-6 py-4 border-t border-gray-100 flex gap-3">
              <button
                onClick={() => { setOverrideModal(null); setOverrideReason('') }}
                className="flex-1 px-4 py-2 border border-gray-200 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50"
              >
                Cancel
              </button>
              <button
                onClick={() => overrideMutation.mutate({ id: overrideModal.id, reason: overrideReason })}
                disabled={!overrideReason.trim() || overrideMutation.isPending}
                className="flex-1 px-4 py-2 bg-blue-600 text-white rounded-lg text-sm font-semibold hover:bg-blue-700 disabled:opacity-50"
              >
                {overrideMutation.isPending ? 'Saving...' : 'Confirm Override'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
