'use client'

import React, { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import Editor from '@monaco-editor/react'
import type { BREDecisionResponse, PagedResponse } from '@/types'
import type { Rule } from '@/types'
import { apiClient } from '@/lib/api-client'
import { AiAnalystPanel } from '../ai-analyst/AiAnalystPanel'

const SAMPLE_PAYLOAD = JSON.stringify({
  applicant: { age: 34, gender: "MALE", pan_number: "ABCDE1234F" },
  employment: { type: "SALARIED", employer_name: "Infosys Ltd", monthly_income: 85000, vintage_months: 48 },
  bureau: {
    cibil_score: 712, experian_score: 708,
    max_dpd_24m: 0, max_dpd_12m: 0,
    total_active_loans: 2,
    total_emi_obligation: 22000,
    written_off_amount: 0,
    suit_filed: false,
    wilful_defaulter: false
  },
  loan: { amount: 750000, tenure_months: 60, purpose: "VEHICLE_PURCHASE" },
  vehicle: { type: "TWO_WHEELER", make: "Honda", model: "Activa 6G", year: 2023, age_years: 0, valuation: 85000 },
  ratios: { foir: 45.8, ltv: 88.2 },
  fi: { verified: true, negative: false, address_match: true, mobile_match: true },
  fraud: { score: 12, blacklisted: false }
}, null, 2)

export function SandboxPage() {
  const [payload, setPayload] = useState(SAMPLE_PAYLOAD)
  const [selectedRuleSetId, setSelectedRuleSetId] = useState<string>('')
  const [result, setResult] = useState<BREDecisionResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<'input' | 'result' | 'ai'>('input')

  const { data: ruleSets } = useQuery({
    queryKey: ['rule-sets'],
    queryFn: () => apiClient.get<{ items: { id: string; setName: string; setCode: string }[] }>('/rule-sets'),
  })

  const simulate = useMutation({
    mutationFn: async () => {
      const parsed = JSON.parse(payload)
      return apiClient.post<BREDecisionResponse>('/simulate-decision', {
        data: parsed,
        ruleSetId: selectedRuleSetId || undefined,
      })
    },
    onSuccess: (data) => {
      setResult(data)
      setError(null)
      setActiveTab('result')
    },
    onError: (err: any) => {
      setError(err.response?.data?.detail ?? err.message)
    },
  })

  const isValidJson = (() => {
    try { JSON.parse(payload); return true } catch { return false }
  })()

  return (
    <div className="h-full flex flex-col">
      {/* Header */}
      <div className="px-6 py-4 border-b border-gray-200 bg-white">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-bold text-gray-900">Rule Testing Sandbox</h1>
            <p className="text-sm text-gray-500 mt-0.5">Test rules before publishing to production</p>
          </div>
          <div className="flex items-center gap-3">
            <select
              value={selectedRuleSetId}
              onChange={(e) => setSelectedRuleSetId(e.target.value)}
              className="text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="">All Published Rules</option>
              {ruleSets?.items.map((rs) => (
                <option key={rs.id} value={rs.id}>{rs.setName}</option>
              ))}
            </select>
            <button
              onClick={() => simulate.mutate()}
              disabled={!isValidJson || simulate.isPending}
              className={`px-6 py-2 rounded-lg text-sm font-semibold text-white transition-colors flex items-center gap-2 ${
                isValidJson && !simulate.isPending
                  ? 'bg-blue-600 hover:bg-blue-700'
                  : 'bg-gray-300 cursor-not-allowed'
              }`}
            >
              {simulate.isPending ? (
                <>
                  <span className="animate-spin">⏳</span> Running...
                </>
              ) : (
                <>▶ Run Simulation</>
              )}
            </button>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-gray-200 bg-white px-6">
        {[
          { id: 'input', label: '📥 Input JSON' },
          { id: 'result', label: `📤 Decision ${result ? `(${result.decision})` : ''}` },
          { id: 'ai', label: '🤖 AI Analysis' },
        ].map(({ id, label }) => (
          <button
            key={id}
            onClick={() => setActiveTab(id as any)}
            disabled={id !== 'input' && !result}
            className={`py-3 px-4 text-sm font-medium border-b-2 transition-colors ${
              activeTab === id
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-gray-500 hover:text-gray-700 disabled:opacity-40'
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {/* Content */}
      <div className="flex-1 overflow-auto">
        {activeTab === 'input' && (
          <div className="h-full flex flex-col">
            <div className="flex-1">
              <Editor
                height="100%"
                defaultLanguage="json"
                value={payload}
                onChange={(v) => setPayload(v ?? '')}
                theme="vs-light"
                options={{
                  minimap: { enabled: false },
                  fontSize: 13,
                  lineNumbers: 'on',
                  scrollBeyondLastLine: false,
                  folding: true,
                  formatOnPaste: true,
                }}
              />
            </div>
            {!isValidJson && (
              <div className="px-6 py-2 bg-red-50 border-t border-red-200 text-sm text-red-700">
                ⚠ Invalid JSON — fix before running simulation
              </div>
            )}
            {error && (
              <div className="px-6 py-3 bg-red-50 border-t border-red-200 text-sm text-red-700">
                ❌ {error}
              </div>
            )}
          </div>
        )}

        {activeTab === 'result' && result && (
          <div className="p-6 space-y-6">
            {/* Decision Header */}
            <DecisionHeader result={result} />

            {/* Rule Results */}
            <div className="bg-white rounded-xl border border-gray-200">
              <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
                <h2 className="font-semibold text-gray-800">Rule Execution Results</h2>
                <div className="flex gap-3 text-sm">
                  <span className="text-emerald-600">{result.rulesPassed} matched</span>
                  <span className="text-gray-400">•</span>
                  <span className="text-gray-500">{result.rulesFailed} not matched</span>
                </div>
              </div>
              <div className="divide-y divide-gray-50">
                {result.ruleResults.map((r, idx) => (
                  <div
                    key={idx}
                    className={`flex items-center gap-4 px-5 py-3 ${r.isMatched ? 'bg-emerald-50/30' : ''}`}
                  >
                    <div className={`w-5 h-5 rounded-full flex items-center justify-center text-xs ${
                      r.isMatched ? 'bg-emerald-500 text-white' : 'bg-gray-200 text-gray-500'
                    }`}>
                      {r.isMatched ? '✓' : '✕'}
                    </div>
                    <div className="flex-1">
                      <div className="text-sm font-medium text-gray-800">{r.ruleName}</div>
                      <div className="text-xs text-gray-400 font-mono">{r.ruleCode}</div>
                    </div>
                    {r.isMatched && r.actionsExecuted.length > 0 && (
                      <div className="flex gap-1 flex-wrap">
                        {r.actionsExecuted.map((a, i) => (
                          <span key={i} className="text-xs bg-blue-100 text-blue-700 px-2 py-0.5 rounded">{a}</span>
                        ))}
                      </div>
                    )}
                    <span className="text-xs text-gray-400 font-mono">{r.executionMs}ms</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}

        {activeTab === 'ai' && result && (
          <div className="p-6">
            <AiAnalystPanel decision={result} />
          </div>
        )}
      </div>
    </div>
  )
}

function DecisionHeader({ result }: { result: BREDecisionResponse }) {
  const decisionColors = {
    APPROVE: 'from-emerald-600 to-emerald-700',
    REJECT: 'from-red-600 to-red-700',
    DEVIATION: 'from-amber-600 to-amber-700',
    REFER: 'from-blue-600 to-blue-700',
    PENDING: 'from-gray-600 to-gray-700',
  }

  return (
    <div className={`bg-gradient-to-r ${decisionColors[result.decision] ?? 'from-gray-600 to-gray-700'} rounded-xl p-6 text-white`}>
      <div className="flex items-center justify-between">
        <div>
          <div className="text-white/70 text-sm font-medium mb-1">FINAL DECISION</div>
          <div className="text-4xl font-black">{result.decision}</div>
          <div className="text-white/70 text-sm mt-1">in {result.executionMs}ms</div>
        </div>
        <div className="text-right">
          <div className="text-5xl font-black">{result.riskScore.toFixed(0)}</div>
          <div className="text-white/70 text-sm">Risk Score /100</div>
          <div className="mt-1 inline-block px-3 py-1 bg-white/20 rounded-full text-sm font-semibold">
            {result.riskCategory} RISK
          </div>
        </div>
      </div>
      <div className="mt-4 pt-4 border-t border-white/20 grid grid-cols-4 gap-4">
        {[
          { label: 'Rules Evaluated', value: result.totalRulesEvaluated },
          { label: 'Rules Matched', value: result.rulesPassed },
          { label: 'Not Matched', value: result.rulesFailed },
          { label: 'Deviations', value: result.deviationsCount },
        ].map(({ label, value }) => (
          <div key={label} className="text-center">
            <div className="text-2xl font-bold">{value}</div>
            <div className="text-white/60 text-xs">{label}</div>
          </div>
        ))}
      </div>
    </div>
  )
}
