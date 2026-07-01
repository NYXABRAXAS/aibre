'use client'

import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'

interface AuditLog {
  id: string
  action: string
  module: string
  entityType: string
  entityId: string
  entityName: string
  userId: string
  userName: string
  userRole: string
  ipAddress: string
  details: string
  status: 'SUCCESS' | 'FAILURE' | 'WARNING'
  timestamp: string
}

const MOCK_LOGS: AuditLog[] = [
  { id: '1', action: 'RULE_PUBLISHED', module: 'Rules', entityType: 'Rule', entityId: 'R-001', entityName: 'CIBIL Score Check', userId: 'USR-001', userName: 'Admin User', userRole: 'Super Admin', ipAddress: '192.168.1.10', details: 'Rule published to production environment. Version 3 → Active.', status: 'SUCCESS', timestamp: '2024-06-28 14:32:10' },
  { id: '2', action: 'DEVIATION_OVERRIDE', module: 'Deviations', entityType: 'Deviation', entityId: 'DEV-128', entityName: 'High FOIR Deviation', userId: 'USR-002', userName: 'Rahul Verma', userRole: 'Branch Manager', ipAddress: '10.0.0.45', details: 'Deviation overridden for REF-2024-001820. Justification: Income verified independently.', status: 'SUCCESS', timestamp: '2024-06-28 13:15:44' },
  { id: '3', action: 'API_KEY_REGENERATED', module: 'Clients', entityType: 'Client', entityId: 'CLT-003', entityName: 'AgriCredit Solutions', userId: 'USR-001', userName: 'Admin User', userRole: 'Super Admin', ipAddress: '192.168.1.10', details: 'API key regenerated. Previous key invalidated immediately.', status: 'SUCCESS', timestamp: '2024-06-28 12:08:22' },
  { id: '4', action: 'LOGIN_FAILED', module: 'Auth', entityType: 'User', entityId: 'USR-004', entityName: 'Unknown', userId: 'UNKNOWN', userName: 'unknown@test.com', userRole: '—', ipAddress: '203.0.113.42', details: 'Failed login attempt. Invalid credentials. Account temporarily locked after 3 attempts.', status: 'FAILURE', timestamp: '2024-06-28 11:45:10' },
  { id: '5', action: 'RULE_SET_CREATED', module: 'Rule Sets', entityType: 'RuleSet', entityId: 'RS-007', entityName: 'CV Loan - Origination', userId: 'USR-003', userName: 'Meena Iyer', userRole: 'Credit Manager', ipAddress: '10.0.0.22', details: 'New rule set created for CV Loan origination. 14 rules added in DRAFT status.', status: 'SUCCESS', timestamp: '2024-06-28 10:22:55' },
  { id: '6', action: 'BRE_EXECUTED', module: 'Execution', entityType: 'Decision', entityId: 'REF-2024-001821', entityName: 'Vehicle Loan Application', userId: 'API', userName: 'API Integration', userRole: 'Client API', ipAddress: '52.14.188.221', details: 'BRE executed via API. 12 rules evaluated. Decision: GREEN. Time: 142ms.', status: 'SUCCESS', timestamp: '2024-06-28 09:55:30' },
  { id: '7', action: 'RULE_DELETED', module: 'Rules', entityType: 'Rule', entityId: 'R-008', entityName: 'Written-Off Amount (Old)', userId: 'USR-001', userName: 'Admin User', userRole: 'Super Admin', ipAddress: '192.168.1.10', details: 'Archived rule permanently deleted. Rule had 489 historical executions.', status: 'WARNING', timestamp: '2024-06-27 18:30:00' },
  { id: '8', action: 'USER_CREATED', module: 'Settings', entityType: 'User', entityId: 'USR-005', entityName: 'Deepa Iyer', userId: 'USR-001', userName: 'Admin User', userRole: 'Super Admin', ipAddress: '192.168.1.10', details: 'New user account created. Role: Underwriter. Welcome email sent.', status: 'SUCCESS', timestamp: '2024-06-27 17:12:44' },
  { id: '9', action: 'PERMISSION_CHANGED', module: 'Settings', entityType: 'Role', entityId: 'ROLE-003', entityName: 'Branch Manager', userId: 'USR-001', userName: 'Admin User', userRole: 'Super Admin', ipAddress: '192.168.1.10', details: 'Permissions updated for Branch Manager role. Added: DEVIATION_OVERRIDE. Removed: RULE_PUBLISH.', status: 'SUCCESS', timestamp: '2024-06-27 15:40:20' },
  { id: '10', action: 'RULE_EXPORTED', module: 'Rules', entityType: 'RuleSet', entityId: 'RS-001', entityName: 'Vehicle Loan - Origination', userId: 'USR-002', userName: 'Rahul Verma', userRole: 'Branch Manager', ipAddress: '10.0.0.45', details: 'Rule set exported as JSON. 12 rules included.', status: 'SUCCESS', timestamp: '2024-06-27 14:05:55' },
]

const STATUS_STYLES: Record<AuditLog['status'], { badge: string; dot: string }> = {
  SUCCESS: { badge: 'badge-green', dot: 'bg-green-500' },
  FAILURE: { badge: 'badge-red', dot: 'bg-red-500' },
  WARNING: { badge: 'badge-amber', dot: 'bg-amber-500' },
}

const MODULE_BADGE: Record<string, string> = {
  Rules: 'badge-blue',
  'Rule Sets': 'badge-blue',
  Deviations: 'badge-amber',
  Auth: 'badge-red',
  Clients: 'badge-green',
  Settings: 'badge-purple',
  Execution: 'badge-green',
}

export function AuditPage() {
  const [search, setSearch] = useState('')
  const [moduleFilter, setModuleFilter] = useState('ALL')
  const [statusFilter, setStatusFilter] = useState('ALL')
  const [expandedId, setExpandedId] = useState<string | null>(null)

  const { data: logs = MOCK_LOGS, isLoading } = useQuery({
    queryKey: ['audit-logs'],
    queryFn: async () => {
      try {
        const res = await apiClient.get<AuditLog[]>('/audit')
        return res.data
      } catch {
        return MOCK_LOGS
      }
    },
  })

  const modules = ['ALL', ...Array.from(new Set(logs.map(l => l.module)))]

  const filtered = logs.filter(l => {
    const matchSearch = l.action.toLowerCase().includes(search.toLowerCase()) ||
      l.userName.toLowerCase().includes(search.toLowerCase()) ||
      l.entityName.toLowerCase().includes(search.toLowerCase()) ||
      l.details.toLowerCase().includes(search.toLowerCase())
    const matchModule = moduleFilter === 'ALL' || l.module === moduleFilter
    const matchStatus = statusFilter === 'ALL' || l.status === statusFilter
    return matchSearch && matchModule && matchStatus
  })

  const stats = {
    total: logs.length,
    success: logs.filter(l => l.status === 'SUCCESS').length,
    failures: logs.filter(l => l.status === 'FAILURE').length,
    warnings: logs.filter(l => l.status === 'WARNING').length,
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Audit Trail</h1>
          <p className="text-gray-500 text-sm mt-1">Complete activity log for compliance and security monitoring</p>
        </div>
        <button className="btn-secondary">Export Audit Log</button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-4 gap-4">
        {[
          { label: 'Total Events', value: stats.total, color: 'text-gray-900' },
          { label: 'Successful', value: stats.success, color: 'text-green-600' },
          { label: 'Failures', value: stats.failures, color: 'text-red-500' },
          { label: 'Warnings', value: stats.warnings, color: 'text-amber-600' },
        ].map(s => (
          <div key={s.label} className="card text-center">
            <p className={`text-3xl font-bold ${s.color}`}>{s.value}</p>
            <p className="text-gray-500 text-sm mt-1">{s.label}</p>
          </div>
        ))}
      </div>

      {/* Filters */}
      <div className="flex gap-3">
        <input type="text" placeholder="Search actions, users, entities..."
          value={search} onChange={e => setSearch(e.target.value)} className="input flex-1" />
        <select value={moduleFilter} onChange={e => setModuleFilter(e.target.value)} className="input w-40">
          {modules.map(m => <option key={m} value={m}>{m === 'ALL' ? 'All Modules' : m}</option>)}
        </select>
        <select value={statusFilter} onChange={e => setStatusFilter(e.target.value)} className="input w-36">
          <option value="ALL">All Status</option>
          <option value="SUCCESS">Success</option>
          <option value="FAILURE">Failure</option>
          <option value="WARNING">Warning</option>
        </select>
      </div>

      {/* Log Table */}
      <div className="card p-0 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 border-b border-gray-200">
            <tr>
              {['Timestamp', 'Action', 'Module', 'Entity', 'User', 'IP Address', 'Status'].map(h => (
                <th key={h} className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {isLoading ? (
              <tr><td colSpan={7} className="text-center py-8 text-gray-400">Loading audit logs...</td></tr>
            ) : filtered.map(log => (
              <>
                <tr key={log.id} className="hover:bg-gray-50 cursor-pointer transition-colors"
                  onClick={() => setExpandedId(expandedId === log.id ? null : log.id)}>
                  <td className="px-4 py-3 text-gray-400 text-xs font-mono whitespace-nowrap">{log.timestamp}</td>
                  <td className="px-4 py-3">
                    <span className="font-medium text-gray-900 font-mono text-xs bg-gray-100 px-2 py-0.5 rounded">{log.action}</span>
                  </td>
                  <td className="px-4 py-3"><span className={MODULE_BADGE[log.module] || 'badge-blue'}>{log.module}</span></td>
                  <td className="px-4 py-3">
                    <p className="text-gray-700 font-medium text-xs">{log.entityName}</p>
                    <p className="text-gray-400 text-xs font-mono">{log.entityId}</p>
                  </td>
                  <td className="px-4 py-3">
                    <p className="text-gray-700 font-medium">{log.userName}</p>
                    <p className="text-gray-400 text-xs">{log.userRole}</p>
                  </td>
                  <td className="px-4 py-3 text-gray-400 text-xs font-mono">{log.ipAddress}</td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1.5">
                      <span className={`w-1.5 h-1.5 rounded-full ${STATUS_STYLES[log.status].dot}`} />
                      <span className={STATUS_STYLES[log.status].badge}>{log.status}</span>
                    </div>
                  </td>
                </tr>
                {expandedId === log.id && (
                  <tr key={`${log.id}-expand`} className="bg-gray-50">
                    <td colSpan={7} className="px-6 py-4">
                      <div className="bg-white rounded-lg p-4 border border-gray-100">
                        <p className="text-xs font-semibold text-gray-500 uppercase mb-2">Event Details</p>
                        <p className="text-gray-700 text-sm">{log.details}</p>
                      </div>
                    </td>
                  </tr>
                )}
              </>
            ))}
          </tbody>
        </table>
        {filtered.length === 0 && (
          <p className="text-center py-8 text-gray-400">No audit logs found</p>
        )}
      </div>
    </div>
  )
}
