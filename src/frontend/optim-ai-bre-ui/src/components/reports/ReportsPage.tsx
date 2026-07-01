'use client'

import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import { BarChart, Bar, LineChart, Line, PieChart, Pie, Cell, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend } from 'recharts'

const MONTHLY_DATA = [
  { month: 'Jan', executions: 8420, approved: 5814, rejected: 1920, amber: 686 },
  { month: 'Feb', executions: 9120, approved: 6384, rejected: 1946, amber: 790 },
  { month: 'Mar', executions: 10840, approved: 7588, rejected: 2168, amber: 1084 },
  { month: 'Apr', executions: 11200, approved: 7840, rejected: 2240, amber: 1120 },
  { month: 'May', executions: 13480, approved: 9436, rejected: 2696, amber: 1348 },
  { month: 'Jun', executions: 15120, approved: 10584, rejected: 3024, amber: 1512 },
]

const PRODUCT_DISTRIBUTION = [
  { name: 'Vehicle Loan', value: 38 },
  { name: 'MSME Loan', value: 22 },
  { name: 'Personal Loan', value: 18 },
  { name: 'Tractor Loan', value: 12 },
  { name: 'CV Loan', value: 7 },
  { name: 'Auto Loan', value: 3 },
]

const TOP_RULES = [
  { rule: 'CIBIL Score Check', hits: 15420, rejectRate: 28 },
  { rule: 'Age Eligibility', hits: 14980, rejectRate: 4 },
  { rule: 'FOIR Calculation', hits: 14210, rejectRate: 18 },
  { rule: 'DPD 24M Check', hits: 12840, rejectRate: 22 },
  { rule: 'Employer Stability', hits: 10920, rejectRate: 12 },
]

const COLORS = ['#3B82F6', '#10B981', '#F59E0B', '#EF4444', '#8B5CF6', '#06B6D4']

const REPORT_TYPES = [
  { id: 'execution', label: '📊 Execution Summary', desc: 'Daily/monthly BRE execution counts and response times' },
  { id: 'decision', label: '⚖️ Decision Analysis', desc: 'Green/Amber/Red breakdown by product and period' },
  { id: 'rules', label: '📋 Rule Performance', desc: 'Hit rates, rejection rates by individual rule' },
  { id: 'deviation', label: '⚠️ Deviation Report', desc: 'Override analysis and deviation trends' },
  { id: 'client', label: '🏢 Client Usage Report', desc: 'Per-client execution volumes and API usage' },
  { id: 'audit', label: '🔍 Audit Log Export', desc: 'Full audit trail with user and timestamp' },
]

export function ReportsPage() {
  const [activeTab, setActiveTab] = useState<'dashboard' | 'generate'>('dashboard')
  const [period, setPeriod] = useState('6M')

  const totalExec = MONTHLY_DATA.reduce((s, m) => s + m.executions, 0)
  const totalApproved = MONTHLY_DATA.reduce((s, m) => s + m.approved, 0)
  const approvalRate = ((totalApproved / totalExec) * 100).toFixed(1)
  const avgExecPerMonth = Math.round(totalExec / MONTHLY_DATA.length)

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Reports & Analytics</h1>
          <p className="text-gray-500 text-sm mt-1">Business intelligence and performance insights</p>
        </div>
        <div className="flex gap-2">
          <select value={period} onChange={e => setPeriod(e.target.value)} className="input w-28">
            <option value="7D">Last 7 Days</option>
            <option value="1M">Last Month</option>
            <option value="3M">Last 3 Months</option>
            <option value="6M">Last 6 Months</option>
            <option value="1Y">Last Year</option>
          </select>
          <button className="btn-secondary">Export PDF</button>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 border-b border-gray-200">
        {[['dashboard', '📊 Analytics Dashboard'], ['generate', '📄 Generate Report']].map(([id, label]) => (
          <button key={id} onClick={() => setActiveTab(id as any)}
            className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${activeTab === id ? 'border-blue-500 text-blue-600' : 'border-transparent text-gray-500 hover:text-gray-700'}`}>
            {label}
          </button>
        ))}
      </div>

      {activeTab === 'dashboard' && (
        <div className="space-y-6">
          {/* KPI Row */}
          <div className="grid grid-cols-4 gap-4">
            {[
              { label: 'Total Executions', value: totalExec.toLocaleString(), sub: `${period} period` },
              { label: 'Approval Rate', value: `${approvalRate}%`, sub: 'GREEN decisions' },
              { label: 'Avg / Month', value: avgExecPerMonth.toLocaleString(), sub: 'executions' },
              { label: 'Active Products', value: '6', sub: 'loan products' },
            ].map(k => (
              <div key={k.label} className="card">
                <p className="text-2xl font-bold text-gray-900">{k.value}</p>
                <p className="text-gray-600 text-sm font-medium mt-1">{k.label}</p>
                <p className="text-gray-400 text-xs">{k.sub}</p>
              </div>
            ))}
          </div>

          {/* Monthly Trend */}
          <div className="card">
            <h3 className="font-semibold text-gray-900 mb-4">Monthly Execution Trend</h3>
            <ResponsiveContainer width="100%" height={280}>
              <BarChart data={MONTHLY_DATA} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                <XAxis dataKey="month" tick={{ fontSize: 12 }} />
                <YAxis tick={{ fontSize: 12 }} />
                <Tooltip />
                <Legend />
                <Bar dataKey="approved" name="Approved" stackId="a" fill="#10B981" radius={[0, 0, 0, 0]} />
                <Bar dataKey="amber" name="Review" stackId="a" fill="#F59E0B" />
                <Bar dataKey="rejected" name="Rejected" stackId="a" fill="#EF4444" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>

          <div className="grid grid-cols-2 gap-6">
            {/* Product Distribution */}
            <div className="card">
              <h3 className="font-semibold text-gray-900 mb-4">Decision by Loan Product</h3>
              <ResponsiveContainer width="100%" height={240}>
                <PieChart>
                  <Pie data={PRODUCT_DISTRIBUTION} cx="50%" cy="50%" outerRadius={80} dataKey="value" label={({ name, value }) => `${name} ${value}%`} labelLine={false}>
                    {PRODUCT_DISTRIBUTION.map((_, i) => <Cell key={i} fill={COLORS[i % COLORS.length]} />)}
                  </Pie>
                  <Tooltip formatter={(v) => `${v}%`} />
                </PieChart>
              </ResponsiveContainer>
            </div>

            {/* Top Rules */}
            <div className="card">
              <h3 className="font-semibold text-gray-900 mb-4">Top Rules by Hit Count</h3>
              <div className="space-y-3">
                {TOP_RULES.map((r, i) => (
                  <div key={i} className="flex items-center gap-3">
                    <span className="w-5 h-5 rounded-full bg-blue-100 text-blue-700 text-xs flex items-center justify-center font-bold flex-shrink-0">{i + 1}</span>
                    <div className="flex-1 min-w-0">
                      <div className="flex justify-between text-xs mb-1">
                        <span className="font-medium text-gray-700 truncate">{r.rule}</span>
                        <span className="text-gray-400 ml-2 flex-shrink-0">{r.hits.toLocaleString()} hits</span>
                      </div>
                      <div className="flex items-center gap-2">
                        <div className="flex-1 bg-gray-100 rounded-full h-1.5">
                          <div className="bg-red-400 h-1.5 rounded-full" style={{ width: `${r.rejectRate}%` }} />
                        </div>
                        <span className="text-xs text-red-500 flex-shrink-0">{r.rejectRate}% reject</span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}

      {activeTab === 'generate' && (
        <div className="space-y-4">
          <p className="text-gray-600 text-sm">Select a report type to generate and download</p>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {REPORT_TYPES.map(r => (
              <div key={r.id} className="card hover:shadow-md transition-shadow cursor-pointer group">
                <h3 className="font-semibold text-gray-900 mb-1">{r.label}</h3>
                <p className="text-gray-400 text-sm mb-4">{r.desc}</p>
                <div className="flex gap-2">
                  <button className="btn-primary flex-1 text-sm">Generate PDF</button>
                  <button className="btn-secondary text-sm">CSV</button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
