'use client'

import { useState } from 'react'

const TABS = [
  { id: 'general', label: '⚙️ General' },
  { id: 'users', label: '👥 Users & Roles' },
  { id: 'api', label: '🔑 API Keys' },
  { id: 'notifications', label: '🔔 Notifications' },
  { id: 'security', label: '🔒 Security' },
]

const MOCK_USERS = [
  { id: '1', name: 'Admin User', email: 'admin@optimai.in', role: 'Super Admin', status: 'ACTIVE', lastLogin: '2024-06-28 14:30' },
  { id: '2', name: 'Rahul Verma', email: 'rahul.verma@demobank.in', role: 'Branch Manager', status: 'ACTIVE', lastLogin: '2024-06-28 13:15' },
  { id: '3', name: 'Meena Iyer', email: 'meena@quickfinance.com', role: 'Credit Manager', status: 'ACTIVE', lastLogin: '2024-06-27 11:20' },
  { id: '4', name: 'Suresh Patil', email: 'suresh@agricredit.in', role: 'Underwriter', status: 'INACTIVE', lastLogin: '2024-05-30 09:00' },
]

const MOCK_API_KEYS = [
  { id: '1', name: 'Production API Key', key: 'brk_live_****_****_****_abcd', created: '2024-01-10', lastUsed: '2024-06-28 14:55', calls: 48201 },
  { id: '2', name: 'Sandbox API Key', key: 'brk_test_****_****_****_efgh', created: '2024-01-10', lastUsed: '2024-06-20 10:00', calls: 1240 },
]

export function SettingsPage() {
  const [tab, setTab] = useState('general')
  const [saved, setSaved] = useState(false)
  const [general, setGeneral] = useState({
    orgName: 'Demo Bank Limited',
    timezone: 'Asia/Kolkata',
    currency: 'INR',
    maxExecutionsPerMin: '1000',
    decisionExpirySec: '300',
    enableAuditLog: true,
    enableAiFeatures: false,
  })

  const handleSave = () => {
    setSaved(true)
    setTimeout(() => setSaved(false), 2000)
  }

  return (
    <div className="p-6 space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Settings</h1>
        <p className="text-gray-500 text-sm mt-1">Configure your OPTIM AI BRE Engine instance</p>
      </div>

      <div className="flex gap-6">
        {/* Sidebar Tabs */}
        <div className="w-48 flex-shrink-0">
          <nav className="space-y-1">
            {TABS.map(t => (
              <button key={t.id} onClick={() => setTab(t.id)}
                className={`w-full text-left px-3 py-2 rounded-lg text-sm font-medium transition-colors ${tab === t.id ? 'bg-blue-50 text-blue-700' : 'text-gray-600 hover:bg-gray-50'}`}>
                {t.label}
              </button>
            ))}
          </nav>
        </div>

        {/* Content */}
        <div className="flex-1">
          {tab === 'general' && (
            <div className="card space-y-5">
              <h2 className="font-semibold text-gray-900 text-lg border-b border-gray-100 pb-3">General Settings</h2>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Organization Name</label>
                  <input className="input w-full" value={general.orgName}
                    onChange={e => setGeneral(p => ({ ...p, orgName: e.target.value }))} />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Timezone</label>
                  <select className="input w-full" value={general.timezone}
                    onChange={e => setGeneral(p => ({ ...p, timezone: e.target.value }))}>
                    <option value="Asia/Kolkata">Asia/Kolkata (IST)</option>
                    <option value="UTC">UTC</option>
                    <option value="Asia/Dubai">Asia/Dubai (GST)</option>
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Currency</label>
                  <select className="input w-full" value={general.currency}
                    onChange={e => setGeneral(p => ({ ...p, currency: e.target.value }))}>
                    <option value="INR">INR (₹)</option>
                    <option value="USD">USD ($)</option>
                    <option value="AED">AED</option>
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Max Executions / Minute</label>
                  <input type="number" className="input w-full" value={general.maxExecutionsPerMin}
                    onChange={e => setGeneral(p => ({ ...p, maxExecutionsPerMin: e.target.value }))} />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Decision Cache TTL (seconds)</label>
                  <input type="number" className="input w-full" value={general.decisionExpirySec}
                    onChange={e => setGeneral(p => ({ ...p, decisionExpirySec: e.target.value }))} />
                </div>
              </div>

              <div className="space-y-3 pt-2 border-t border-gray-100">
                <h3 className="font-medium text-gray-700">Feature Toggles</h3>
                {[
                  { key: 'enableAuditLog', label: 'Enable Audit Logging', desc: 'Log all user and API actions to audit trail' },
                  { key: 'enableAiFeatures', label: 'Enable AI Features', desc: 'AI-powered rule generation and credit analysis' },
                ].map(f => (
                  <div key={f.key} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                    <div>
                      <p className="font-medium text-gray-800 text-sm">{f.label}</p>
                      <p className="text-gray-400 text-xs">{f.desc}</p>
                    </div>
                    <button
                      onClick={() => setGeneral(p => ({ ...p, [f.key]: !p[f.key as keyof typeof p] }))}
                      className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${general[f.key as keyof typeof general] ? 'bg-blue-600' : 'bg-gray-300'}`}>
                      <span className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${general[f.key as keyof typeof general] ? 'translate-x-6' : 'translate-x-1'}`} />
                    </button>
                  </div>
                ))}
              </div>

              <div className="flex justify-end gap-2 pt-2">
                <button className="btn-secondary">Reset</button>
                <button onClick={handleSave} className="btn-primary">
                  {saved ? '✓ Saved!' : 'Save Changes'}
                </button>
              </div>
            </div>
          )}

          {tab === 'users' && (
            <div className="card">
              <div className="flex items-center justify-between mb-4">
                <h2 className="font-semibold text-gray-900 text-lg">Users & Roles</h2>
                <button className="btn-primary text-sm">+ Add User</button>
              </div>
              <table className="w-full text-sm">
                <thead className="bg-gray-50">
                  <tr>
                    {['Name', 'Email', 'Role', 'Status', 'Last Login', 'Actions'].map(h => (
                      <th key={h} className="text-left px-3 py-2 text-xs font-semibold text-gray-500">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {MOCK_USERS.map(u => (
                    <tr key={u.id} className="hover:bg-gray-50">
                      <td className="px-3 py-3 font-medium text-gray-900">{u.name}</td>
                      <td className="px-3 py-3 text-gray-500 text-xs">{u.email}</td>
                      <td className="px-3 py-3"><span className="badge-blue">{u.role}</span></td>
                      <td className="px-3 py-3">
                        <span className={u.status === 'ACTIVE' ? 'badge-green' : 'badge-red'}>{u.status}</span>
                      </td>
                      <td className="px-3 py-3 text-gray-400 text-xs">{u.lastLogin}</td>
                      <td className="px-3 py-3">
                        <div className="flex gap-2">
                          <button className="text-blue-600 hover:text-blue-800 text-xs font-medium">Edit</button>
                          <button className="text-gray-400 hover:text-red-600 text-xs font-medium">Deactivate</button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {tab === 'api' && (
            <div className="card space-y-4">
              <div className="flex items-center justify-between">
                <h2 className="font-semibold text-gray-900 text-lg">API Keys</h2>
                <button className="btn-primary text-sm">+ Generate Key</button>
              </div>
              <div className="space-y-3">
                {MOCK_API_KEYS.map(k => (
                  <div key={k.id} className="border border-gray-200 rounded-lg p-4">
                    <div className="flex items-start justify-between mb-2">
                      <div>
                        <p className="font-semibold text-gray-900">{k.name}</p>
                        <p className="text-gray-400 text-xs mt-0.5">Created {k.created} · {k.calls.toLocaleString()} API calls</p>
                      </div>
                      <div className="flex gap-2">
                        <button className="btn-secondary text-xs">Rotate</button>
                        <button className="text-red-500 hover:text-red-700 text-xs font-medium">Revoke</button>
                      </div>
                    </div>
                    <div className="flex items-center gap-2 bg-gray-900 rounded-lg px-3 py-2 font-mono text-sm">
                      <span className="text-green-400 flex-1">{k.key}</span>
                      <button className="text-gray-400 hover:text-white text-xs">Copy</button>
                    </div>
                    <p className="text-gray-400 text-xs mt-2">Last used: {k.lastUsed}</p>
                  </div>
                ))}
              </div>
            </div>
          )}

          {tab === 'notifications' && (
            <div className="card space-y-4">
              <h2 className="font-semibold text-gray-900 text-lg">Notification Settings</h2>
              <div className="space-y-3">
                {[
                  { label: 'Rule Published', desc: 'Notify when a rule is published to production' },
                  { label: 'High Deviation Rate', desc: 'Alert when deviation rate exceeds 15%' },
                  { label: 'API Error Spike', desc: 'Alert when API error rate exceeds 5%' },
                  { label: 'New Client Onboarded', desc: 'Notify when a new client is activated' },
                  { label: 'Failed Login Attempts', desc: 'Security alert for multiple failed logins' },
                  { label: 'Daily Execution Summary', desc: 'Daily digest of BRE executions' },
                ].map((n, i) => (
                  <div key={i} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                    <div>
                      <p className="font-medium text-gray-800 text-sm">{n.label}</p>
                      <p className="text-gray-400 text-xs">{n.desc}</p>
                    </div>
                    <div className="flex gap-3">
                      {['Email', 'Slack'].map(ch => (
                        <label key={ch} className="flex items-center gap-1.5 cursor-pointer">
                          <input type="checkbox" defaultChecked={ch === 'Email'} className="w-3.5 h-3.5 text-blue-600 rounded" />
                          <span className="text-xs text-gray-600">{ch}</span>
                        </label>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
              <div className="flex justify-end">
                <button onClick={handleSave} className="btn-primary">{saved ? '✓ Saved!' : 'Save Preferences'}</button>
              </div>
            </div>
          )}

          {tab === 'security' && (
            <div className="card space-y-5">
              <h2 className="font-semibold text-gray-900 text-lg">Security Settings</h2>
              <div className="space-y-3">
                {[
                  { label: 'Two-Factor Authentication', desc: 'Require 2FA for all admin users', enabled: true },
                  { label: 'IP Allowlisting', desc: 'Restrict API access to whitelisted IPs', enabled: false },
                  { label: 'Session Timeout', desc: 'Automatically log out inactive users after 30 minutes', enabled: true },
                  { label: 'Audit Log Retention', desc: 'Keep audit logs for 365 days', enabled: true },
                  { label: 'API Rate Limiting', desc: 'Enforce rate limits per API key', enabled: true },
                ].map((s, i) => (
                  <div key={i} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                    <div>
                      <p className="font-medium text-gray-800 text-sm">{s.label}</p>
                      <p className="text-gray-400 text-xs">{s.desc}</p>
                    </div>
                    <button className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${s.enabled ? 'bg-blue-600' : 'bg-gray-300'}`}>
                      <span className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${s.enabled ? 'translate-x-6' : 'translate-x-1'}`} />
                    </button>
                  </div>
                ))}
              </div>
              <div className="border border-red-200 rounded-lg p-4 bg-red-50">
                <h3 className="font-semibold text-red-700 mb-2">Danger Zone</h3>
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-red-700 font-medium text-sm">Reset All Settings</p>
                    <p className="text-red-500 text-xs">This will reset all configuration to factory defaults</p>
                  </div>
                  <button className="px-3 py-1.5 bg-red-600 text-white text-sm rounded-lg hover:bg-red-700 transition-colors">
                    Reset
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
