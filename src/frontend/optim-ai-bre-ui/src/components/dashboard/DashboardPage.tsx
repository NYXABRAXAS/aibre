'use client'

import React from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  LineChart, Line, BarChart, Bar, PieChart, Pie, Cell,
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend
} from 'recharts'
import type { DashboardStats, ExecutionTrend, RuleHitAnalysis, DeviationAnalysisSummary } from '@/types'
import { apiClient } from '@/lib/api-client'

const TRAFFIC_COLORS = {
  approved: '#10b981',
  rejected: '#ef4444',
  deviations: '#f59e0b',
}

export function DashboardPage() {
  const { data: stats } = useQuery<DashboardStats>({
    queryKey: ['dashboard-stats'],
    queryFn: () => apiClient.get('/analytics/dashboard-stats'),
    refetchInterval: 30_000,
  })

  const { data: trends } = useQuery<ExecutionTrend[]>({
    queryKey: ['execution-trends'],
    queryFn: () => apiClient.get('/analytics/execution-trends?days=30'),
  })

  const { data: topRules } = useQuery<RuleHitAnalysis[]>({
    queryKey: ['rule-hit-analysis'],
    queryFn: () => apiClient.get('/analytics/rule-hit-analysis?limit=10'),
  })

  const { data: deviations } = useQuery<DeviationAnalysisSummary[]>({
    queryKey: ['deviation-analysis'],
    queryFn: () => apiClient.get('/analytics/deviation-analysis'),
  })

  const decisionData = stats
    ? [
        { name: 'Approved', value: stats.approvalRate, color: '#10b981' },
        { name: 'Rejected', value: stats.rejectionRate, color: '#ef4444' },
        { name: 'Deviation', value: stats.deviationRate, color: '#f59e0b' },
      ]
    : []

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">OPTIM AI BRE Dashboard</h1>
          <p className="text-gray-500 text-sm mt-1">Real-time rule engine analytics and insights</p>
        </div>
        <div className="flex items-center gap-2">
          <div className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
          <span className="text-sm text-gray-500">Live</span>
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <KPICard
          label="Total Executions Today"
          value={stats?.todayExecutions?.toLocaleString() ?? '—'}
          sub={`${stats?.totalExecutions?.toLocaleString() ?? '—'} total`}
          color="blue"
          icon="⚡"
        />
        <KPICard
          label="Approval Rate"
          value={`${stats?.approvalRate?.toFixed(1) ?? '—'}%`}
          sub="GREEN decisions"
          color="emerald"
          icon="✅"
        />
        <KPICard
          label="Rejection Rate"
          value={`${stats?.rejectionRate?.toFixed(1) ?? '—'}%`}
          sub="RED decisions"
          color="red"
          icon="🚫"
        />
        <KPICard
          label="Avg Risk Score"
          value={stats?.avgRiskScore?.toFixed(1) ?? '—'}
          sub="/100"
          color="amber"
          icon="📊"
        />
      </div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <KPICard
          label="Published Rules"
          value={stats?.publishedRules?.toLocaleString() ?? '—'}
          sub={`${stats?.activeRules ?? '—'} active`}
          color="purple"
          icon="📜"
        />
        <KPICard
          label="Deviation Rate"
          value={`${stats?.deviationRate?.toFixed(1) ?? '—'}%`}
          sub="AMBER decisions"
          color="orange"
          icon="⚠️"
        />
        <KPICard
          label="Avg Execution Time"
          value={`${stats?.avgExecutionMs?.toFixed(0) ?? '—'}ms`}
          sub="per decision"
          color="teal"
          icon="⏱️"
        />
        <KPICard
          label="Total Rules"
          value={stats?.totalRules?.toLocaleString() ?? '—'}
          sub={`${stats?.draftRules ?? '—'} in draft`}
          color="indigo"
          icon="⚙️"
        />
      </div>

      {/* Charts Row 1 */}
      <div className="grid grid-cols-3 gap-6">
        {/* Execution Trend */}
        <div className="col-span-2 bg-white rounded-xl border border-gray-200 p-5">
          <h2 className="text-base font-semibold text-gray-800 mb-4">Decision Trend (30 Days)</h2>
          <ResponsiveContainer width="100%" height={220}>
            <LineChart data={trends ?? []}>
              <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
              <XAxis dataKey="date" tick={{ fontSize: 11 }} />
              <YAxis tick={{ fontSize: 11 }} />
              <Tooltip />
              <Legend />
              <Line type="monotone" dataKey="approved" name="Approved" stroke="#10b981" strokeWidth={2} dot={false} />
              <Line type="monotone" dataKey="rejected" name="Rejected" stroke="#ef4444" strokeWidth={2} dot={false} />
              <Line type="monotone" dataKey="deviations" name="Deviations" stroke="#f59e0b" strokeWidth={2} dot={false} />
            </LineChart>
          </ResponsiveContainer>
        </div>

        {/* Decision Distribution */}
        <div className="bg-white rounded-xl border border-gray-200 p-5">
          <h2 className="text-base font-semibold text-gray-800 mb-4">Decision Distribution</h2>
          <ResponsiveContainer width="100%" height={180}>
            <PieChart>
              <Pie
                data={decisionData}
                cx="50%"
                cy="50%"
                innerRadius={50}
                outerRadius={80}
                paddingAngle={3}
                dataKey="value"
              >
                {decisionData.map((entry, idx) => (
                  <Cell key={idx} fill={entry.color} />
                ))}
              </Pie>
              <Tooltip formatter={(v) => `${Number(v).toFixed(1)}%`} />
            </PieChart>
          </ResponsiveContainer>
          <div className="space-y-1 mt-2">
            {decisionData.map((d) => (
              <div key={d.name} className="flex items-center justify-between text-sm">
                <div className="flex items-center gap-2">
                  <div className="w-3 h-3 rounded-full" style={{ backgroundColor: d.color }} />
                  <span className="text-gray-600">{d.name}</span>
                </div>
                <span className="font-semibold text-gray-800">{d.value?.toFixed(1)}%</span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Charts Row 2 */}
      <div className="grid grid-cols-2 gap-6">
        {/* Top Rules by Hit Rate */}
        <div className="bg-white rounded-xl border border-gray-200 p-5">
          <h2 className="text-base font-semibold text-gray-800 mb-4">Top Rules by Hit Rate</h2>
          <ResponsiveContainer width="100%" height={220}>
            <BarChart data={topRules?.slice(0, 8) ?? []} layout="vertical">
              <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
              <XAxis type="number" tick={{ fontSize: 11 }} unit="%" />
              <YAxis type="category" dataKey="ruleName" tick={{ fontSize: 10 }} width={120} />
              <Tooltip formatter={(v) => `${Number(v).toFixed(1)}%`} />
              <Bar dataKey="hitRate" name="Hit Rate" fill="#6366f1" radius={[0, 4, 4, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>

        {/* Top Deviations */}
        <div className="bg-white rounded-xl border border-gray-200 p-5">
          <h2 className="text-base font-semibold text-gray-800 mb-1">Top Deviations</h2>
          <p className="text-xs text-gray-400 mb-4">Most frequent policy deviations</p>
          <div className="space-y-3 max-h-60 overflow-auto">
            {(deviations ?? []).slice(0, 10).map((d, idx) => (
              <div key={d.deviationCode} className="flex items-center gap-3">
                <span className="text-xs font-bold text-gray-400 w-5">{idx + 1}</span>
                <div className="flex-1">
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-medium text-gray-700 truncate">{d.deviationName}</span>
                    <SeverityBadge severity={d.severity} />
                  </div>
                  <div className="mt-1 h-1.5 bg-gray-100 rounded-full overflow-hidden">
                    <div
                      className="h-full bg-orange-400 rounded-full"
                      style={{ width: `${Math.min((d.occurrenceCount / ((deviations?.[0]?.occurrenceCount ?? 1))) * 100, 100)}%` }}
                    />
                  </div>
                </div>
                <span className="text-xs text-gray-500 font-mono">{d.occurrenceCount}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}

function KPICard({
  label, value, sub, color, icon
}: {
  label: string; value: string; sub: string; color: string; icon: string
}) {
  const colorMap: Record<string, string> = {
    blue: 'bg-blue-50 text-blue-700',
    emerald: 'bg-emerald-50 text-emerald-700',
    red: 'bg-red-50 text-red-700',
    amber: 'bg-amber-50 text-amber-700',
    purple: 'bg-purple-50 text-purple-700',
    orange: 'bg-orange-50 text-orange-700',
    teal: 'bg-teal-50 text-teal-700',
    indigo: 'bg-indigo-50 text-indigo-700',
  }

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-start justify-between">
        <div>
          <p className="text-xs text-gray-500 font-medium">{label}</p>
          <p className="text-2xl font-bold text-gray-900 mt-1">{value}</p>
          <p className="text-xs text-gray-400 mt-0.5">{sub}</p>
        </div>
        <div className={`w-9 h-9 rounded-lg flex items-center justify-center text-lg ${colorMap[color] ?? 'bg-gray-50'}`}>
          {icon}
        </div>
      </div>
    </div>
  )
}

function SeverityBadge({ severity }: { severity: string }) {
  const colors: Record<string, string> = {
    LOW: 'bg-blue-100 text-blue-700',
    MEDIUM: 'bg-yellow-100 text-yellow-700',
    HIGH: 'bg-orange-100 text-orange-700',
    CRITICAL: 'bg-red-100 text-red-700',
  }
  return (
    <span className={`text-xs px-1.5 py-0.5 rounded font-semibold ${colors[severity] ?? 'bg-gray-100'}`}>
      {severity}
    </span>
  )
}
