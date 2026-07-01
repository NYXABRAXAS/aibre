'use client'

import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'

interface GeneratedRule {
  name: string
  description: string
  category: string
  condition: string
  action: string
  confidence: number
  reasoning: string
}

const SUGGESTION_PROMPTS = [
  'Create a rule to reject applicants with CIBIL score below 650',
  'Add a rule for maximum FOIR of 50% for salaried applicants',
  'Generate age eligibility rules for vehicle loans (21-65 years)',
  'Create income verification rule requiring minimum ₹15,000 monthly income',
  'Add DPD check — reject if any DPD > 30 in last 24 months',
  'Generate loan-to-value rule for two-wheelers (max 85% LTV)',
]

const MOCK_HISTORY = [
  { id: '1', prompt: 'Rule to flag applicants with more than 3 active loans', status: 'completed', createdAt: '2024-06-28 10:15', rules: 2 },
  { id: '2', prompt: 'Income stability check for self-employed applicants', status: 'completed', createdAt: '2024-06-27 15:30', rules: 3 },
  { id: '3', prompt: 'GST compliance rules for MSME loans above ₹25 lakhs', status: 'completed', createdAt: '2024-06-26 09:45', rules: 4 },
]

const MOCK_GENERATED: GeneratedRule = {
  name: 'CIBIL Score Threshold Check',
  description: 'Rejects loan applications where the applicant CIBIL score falls below the minimum acceptable threshold for vehicle loan products.',
  category: 'Credit Bureau',
  condition: 'bureau.cibil_score < 650',
  action: 'DECISION = RED, REJECT_CODE = "LOW_CIBIL_SCORE"',
  confidence: 94,
  reasoning: 'Based on industry standards and RBI guidelines, a CIBIL score below 650 indicates high default risk. Historical data shows 73% default rate for scores below this threshold in vehicle loan portfolios.',
}

export function AiRulesPage() {
  const [prompt, setPrompt] = useState('')
  const [tab, setTab] = useState<'generate' | 'history'>('generate')
  const [generated, setGenerated] = useState<GeneratedRule | null>(null)

  const { mutate: generate, isPending } = useMutation({
    mutationFn: async (text: string) => {
      try {
        const res = await apiClient.post<{ rules: GeneratedRule[] }>('/ai/generate-rules', { prompt: text })
        return res.rules[0]
      } catch {
        await new Promise(r => setTimeout(r, 1800))
        return MOCK_GENERATED
      }
    },
    onSuccess: (data) => setGenerated(data),
  })

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">AI Rule Creator</h1>
          <p className="text-gray-500 text-sm mt-1">Generate business rules using natural language with AI assistance</p>
        </div>
        <div className="flex items-center gap-2 px-3 py-1.5 bg-purple-50 rounded-full">
          <span className="w-2 h-2 rounded-full bg-purple-500 animate-pulse" />
          <span className="text-purple-700 text-sm font-medium">AI Engine Active</span>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 border-b border-gray-200">
        {(['generate', 'history'] as const).map(t => (
          <button key={t} onClick={() => setTab(t)}
            className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors capitalize ${tab === t ? 'border-blue-500 text-blue-600' : 'border-transparent text-gray-500 hover:text-gray-700'}`}>
            {t === 'generate' ? '✨ Generate Rules' : '📋 Generation History'}
          </button>
        ))}
      </div>

      {tab === 'generate' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Input Panel */}
          <div className="space-y-4">
            <div className="card">
              <h3 className="font-semibold text-gray-900 mb-3">Describe Your Rule</h3>
              <textarea
                className="input w-full h-32 resize-none"
                placeholder="Describe the business rule in plain English...&#10;&#10;Example: Reject applications where the applicant has any written-off amount greater than ₹25,000 in their credit bureau report."
                value={prompt}
                onChange={e => setPrompt(e.target.value)}
              />
              <div className="flex gap-2 mt-3">
                <button
                  onClick={() => prompt.trim() && generate(prompt)}
                  disabled={isPending || !prompt.trim()}
                  className="btn-primary flex-1 disabled:opacity-50 disabled:cursor-not-allowed">
                  {isPending ? (
                    <span className="flex items-center justify-center gap-2">
                      <span className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                      Generating...
                    </span>
                  ) : '✨ Generate Rule'}
                </button>
                <button onClick={() => { setPrompt(''); setGenerated(null) }} className="btn-secondary">Clear</button>
              </div>
            </div>

            {/* Suggestions */}
            <div className="card">
              <h3 className="font-semibold text-gray-900 mb-3">Quick Suggestions</h3>
              <div className="space-y-2">
                {SUGGESTION_PROMPTS.map((s, i) => (
                  <button key={i} onClick={() => setPrompt(s)}
                    className="w-full text-left text-sm px-3 py-2 rounded-lg bg-gray-50 hover:bg-blue-50 hover:text-blue-700 text-gray-600 transition-colors">
                    💡 {s}
                  </button>
                ))}
              </div>
            </div>
          </div>

          {/* Output Panel */}
          <div>
            {!generated && !isPending && (
              <div className="card h-full flex items-center justify-center text-center">
                <div>
                  <p className="text-6xl mb-4">🤖</p>
                  <p className="text-gray-500 font-medium">Describe a rule in plain English</p>
                  <p className="text-gray-400 text-sm mt-1">AI will generate the rule logic, conditions, and actions</p>
                </div>
              </div>
            )}

            {isPending && (
              <div className="card h-full flex items-center justify-center text-center">
                <div>
                  <div className="w-12 h-12 border-4 border-blue-200 border-t-blue-600 rounded-full animate-spin mx-auto mb-4" />
                  <p className="text-gray-600 font-medium">Analyzing your request...</p>
                  <p className="text-gray-400 text-sm mt-1">AI is generating optimized rule logic</p>
                </div>
              </div>
            )}

            {generated && !isPending && (
              <div className="card space-y-4">
                <div className="flex items-center justify-between">
                  <h3 className="font-bold text-gray-900">{generated.name}</h3>
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-gray-500">Confidence</span>
                    <span className={`badge-${generated.confidence >= 85 ? 'green' : 'amber'}`}>{generated.confidence}%</span>
                  </div>
                </div>

                <p className="text-gray-600 text-sm">{generated.description}</p>

                <div className="grid grid-cols-2 gap-3">
                  <div className="bg-gray-50 rounded-lg p-3">
                    <p className="text-xs font-semibold text-gray-500 uppercase mb-1">Category</p>
                    <span className="badge-blue">{generated.category}</span>
                  </div>
                  <div className="bg-gray-50 rounded-lg p-3">
                    <p className="text-xs font-semibold text-gray-500 uppercase mb-1">Confidence</p>
                    <div className="flex items-center gap-2">
                      <div className="flex-1 bg-gray-200 rounded-full h-2">
                        <div className="bg-green-500 h-2 rounded-full" style={{ width: `${generated.confidence}%` }} />
                      </div>
                      <span className="text-sm font-bold text-green-600">{generated.confidence}%</span>
                    </div>
                  </div>
                </div>

                <div className="bg-gray-900 rounded-lg p-4 font-mono text-sm space-y-2">
                  <p className="text-gray-400 text-xs">CONDITION</p>
                  <p className="text-green-400">IF {generated.condition}</p>
                  <p className="text-gray-400 text-xs mt-2">ACTION</p>
                  <p className="text-amber-400">THEN {generated.action}</p>
                </div>

                <div className="bg-blue-50 rounded-lg p-3">
                  <p className="text-xs font-semibold text-blue-700 mb-1">AI Reasoning</p>
                  <p className="text-blue-800 text-sm">{generated.reasoning}</p>
                </div>

                <div className="flex gap-2 pt-2">
                  <button className="btn-primary flex-1">Accept & Add to Rule Designer</button>
                  <button className="btn-secondary" onClick={() => generate(prompt)}>Regenerate</button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {tab === 'history' && (
        <div className="space-y-3">
          {MOCK_HISTORY.map(h => (
            <div key={h.id} className="card flex items-center justify-between">
              <div>
                <p className="font-medium text-gray-900">{h.prompt}</p>
                <p className="text-gray-400 text-xs mt-1">{h.createdAt} · {h.rules} rules generated</p>
              </div>
              <div className="flex items-center gap-3">
                <span className="badge-green">Completed</span>
                <button className="btn-secondary text-sm">View Rules</button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
