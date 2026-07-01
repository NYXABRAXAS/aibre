'use client'

import React, { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import type { AiAnalysis, BREDecisionResponse, Deviation } from '@/types'
import { apiClient } from '@/lib/api-client'

interface Props {
  decision: BREDecisionResponse
}

export function AiAnalystPanel({ decision }: Props) {
  const [aiResult, setAiResult] = useState<AiAnalysis | null>(decision.aiAnalysis ?? null)
  const [activeSection, setActiveSection] = useState<string>('summary')

  const analyze = useMutation({
    mutationFn: () =>
      apiClient.post('/ai/analyze-credit', {
        applicationData: {},
        decision: decision.decision,
        riskScore: decision.riskScore,
        riskCategory: decision.riskCategory,
        deviations: [],
      }) as Promise<AiAnalysis>,
    onSuccess: setAiResult,
  })

  const sections = [
    { id: 'summary', label: 'Risk Summary', icon: '📊' },
    { id: 'credit', label: 'Credit Analysis', icon: '💳' },
    { id: 'strengths', label: 'Strengths', icon: '💪' },
    { id: 'weaknesses', label: 'Weaknesses', icon: '⚠️' },
    { id: 'deviations', label: 'Deviations', icon: '📋' },
    { id: 'docs', label: 'Documents Required', icon: '📂' },
    { id: 'notes', label: 'Underwriting Notes', icon: '📝' },
  ]

  return (
    <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
      {/* Header */}
      <div className="bg-gradient-to-r from-violet-600 to-purple-700 px-6 py-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-white/20 flex items-center justify-center text-xl">🤖</div>
            <div>
              <h2 className="text-white font-bold text-lg">OPTIM AI Credit Analyst</h2>
              <p className="text-purple-200 text-sm">AI-powered credit risk assessment</p>
            </div>
          </div>
          {!aiResult && (
            <button
              onClick={() => analyze.mutate()}
              disabled={analyze.isPending}
              className="px-4 py-2 bg-white text-purple-700 rounded-lg text-sm font-semibold hover:bg-purple-50 transition-colors disabled:opacity-60"
            >
              {analyze.isPending ? '⏳ Analyzing...' : '✨ Run AI Analysis'}
            </button>
          )}
          {aiResult && (
            <div className="flex items-center gap-2">
              <div className="text-white/70 text-sm">Confidence:</div>
              <div className="text-white font-bold">{Math.round(aiResult.confidenceScore * 100)}%</div>
            </div>
          )}
        </div>
      </div>

      {/* Decision Summary Bar */}
      <div className={`px-6 py-3 flex items-center gap-6 ${
        decision.decision === 'APPROVE' ? 'bg-emerald-50 border-b border-emerald-100' :
        decision.decision === 'REJECT' ? 'bg-red-50 border-b border-red-100' :
        'bg-amber-50 border-b border-amber-100'
      }`}>
        <TrafficLightIndicator light={decision.trafficLight} />
        <div className="flex gap-6 text-sm">
          <div>
            <span className="text-gray-500">Decision:</span>
            <span className={`ml-2 font-bold ${
              decision.decision === 'APPROVE' ? 'text-emerald-700' :
              decision.decision === 'REJECT' ? 'text-red-700' : 'text-amber-700'
            }`}>{decision.decision}</span>
          </div>
          <div>
            <span className="text-gray-500">Risk Score:</span>
            <span className="ml-2 font-bold text-gray-800">{decision.riskScore.toFixed(1)}/100</span>
          </div>
          <div>
            <span className="text-gray-500">Risk:</span>
            <RiskBadge category={decision.riskCategory} />
          </div>
          <div>
            <span className="text-gray-500">Rules:</span>
            <span className="ml-2 font-bold text-gray-800">
              {decision.rulesPassed}/{decision.totalRulesEvaluated} matched
            </span>
          </div>
          <div>
            <span className="text-gray-500">Deviations:</span>
            <span className="ml-2 font-bold text-amber-700">{decision.deviationsCount}</span>
          </div>
          <div>
            <span className="text-gray-500">Time:</span>
            <span className="ml-2 font-bold text-gray-800">{decision.executionMs}ms</span>
          </div>
        </div>
      </div>

      {aiResult ? (
        <div className="flex h-[500px]">
          {/* Sidebar Nav */}
          <div className="w-48 bg-gray-50 border-r border-gray-100 py-4">
            {sections.map((s) => (
              <button
                key={s.id}
                onClick={() => setActiveSection(s.id)}
                className={`w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left transition-colors ${
                  activeSection === s.id
                    ? 'bg-violet-50 text-violet-700 font-semibold border-r-2 border-violet-600'
                    : 'text-gray-600 hover:bg-gray-100'
                }`}
              >
                <span>{s.icon}</span>
                <span>{s.label}</span>
              </button>
            ))}
          </div>

          {/* Content */}
          <div className="flex-1 overflow-auto p-6">
            {activeSection === 'summary' && (
              <AiSection title="Risk Summary" icon="📊" content={aiResult.riskSummary} />
            )}
            {activeSection === 'credit' && (
              <AiSection title="Credit Summary" icon="💳" content={aiResult.creditSummary} />
            )}
            {activeSection === 'strengths' && (
              <ListSection title="Customer Strengths" icon="💪" items={aiResult.strengths} variant="success" />
            )}
            {activeSection === 'weaknesses' && (
              <ListSection title="Customer Weaknesses" icon="⚠️" items={aiResult.weaknesses} variant="warning" />
            )}
            {activeSection === 'deviations' && (
              <div>
                <AiSection title="Deviation Summary" icon="📋" content={aiResult.deviationsSummary} />
                {aiResult.rejectionReasons.length > 0 && (
                  <div className="mt-4">
                    <ListSection title="Rejection Reasons" icon="🚫" items={aiResult.rejectionReasons} variant="error" />
                  </div>
                )}
              </div>
            )}
            {activeSection === 'docs' && (
              <ListSection title="Additional Documents Required" icon="📂" items={aiResult.additionalDocuments} variant="info" />
            )}
            {activeSection === 'notes' && (
              <AiSection title="Underwriting Notes" icon="📝" content={aiResult.underwritingNotes} />
            )}
          </div>
        </div>
      ) : (
        <div className="flex flex-col items-center justify-center py-16 text-gray-400">
          <div className="text-5xl mb-4">🤖</div>
          <p className="text-lg font-medium text-gray-500">AI Analysis Not Yet Run</p>
          <p className="text-sm mt-1">Click "Run AI Analysis" to get comprehensive credit insights</p>
        </div>
      )}
    </div>
  )
}

function AiSection({ title, icon, content }: { title: string; icon: string; content: string }) {
  return (
    <div>
      <h3 className="flex items-center gap-2 text-base font-semibold text-gray-800 mb-3">
        <span>{icon}</span> {title}
      </h3>
      <div className="bg-gray-50 rounded-xl p-4 text-sm text-gray-700 leading-relaxed whitespace-pre-wrap">
        {content || <span className="text-gray-400 italic">No content available</span>}
      </div>
    </div>
  )
}

function ListSection({
  title, icon, items, variant
}: {
  title: string; icon: string; items: string[]; variant: 'success' | 'warning' | 'error' | 'info'
}) {
  const styles = {
    success: { dot: 'bg-emerald-500', bg: 'bg-emerald-50', border: 'border-emerald-100' },
    warning: { dot: 'bg-amber-500', bg: 'bg-amber-50', border: 'border-amber-100' },
    error: { dot: 'bg-red-500', bg: 'bg-red-50', border: 'border-red-100' },
    info: { dot: 'bg-blue-500', bg: 'bg-blue-50', border: 'border-blue-100' },
  }[variant]

  return (
    <div>
      <h3 className="flex items-center gap-2 text-base font-semibold text-gray-800 mb-3">
        <span>{icon}</span> {title}
      </h3>
      {items.length === 0 ? (
        <p className="text-gray-400 text-sm italic">None identified</p>
      ) : (
        <div className={`rounded-xl border ${styles.border} ${styles.bg} p-4 space-y-2`}>
          {items.map((item, idx) => (
            <div key={idx} className="flex items-start gap-2">
              <div className={`w-2 h-2 rounded-full ${styles.dot} mt-1.5 flex-shrink-0`} />
              <span className="text-sm text-gray-700">{item}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function TrafficLightIndicator({ light }: { light: string }) {
  return (
    <div className="flex items-center gap-1.5">
      {(['GREEN', 'AMBER', 'RED'] as const).map((l) => (
        <div
          key={l}
          className={`w-4 h-4 rounded-full transition-all ${
            l === light
              ? l === 'GREEN' ? 'bg-emerald-500 shadow-lg shadow-emerald-200' :
                l === 'AMBER' ? 'bg-amber-500 shadow-lg shadow-amber-200' :
                'bg-red-500 shadow-lg shadow-red-200'
              : 'bg-gray-200'
          }`}
        />
      ))}
    </div>
  )
}

function RiskBadge({ category }: { category: string }) {
  const colors: Record<string, string> = {
    LOW: 'ml-2 px-2 py-0.5 bg-emerald-100 text-emerald-700 rounded text-xs font-semibold',
    MEDIUM: 'ml-2 px-2 py-0.5 bg-blue-100 text-blue-700 rounded text-xs font-semibold',
    HIGH: 'ml-2 px-2 py-0.5 bg-orange-100 text-orange-700 rounded text-xs font-semibold',
    CRITICAL: 'ml-2 px-2 py-0.5 bg-red-100 text-red-700 rounded text-xs font-semibold',
  }
  return <span className={colors[category] ?? colors.MEDIUM}>{category}</span>
}
