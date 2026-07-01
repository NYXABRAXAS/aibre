'use client'

import React from 'react'
import type { RuleAction, ActionType, Decision, RiskCategory, TrafficLight, Severity } from '@/types'

interface Props {
  action: RuleAction
  index: number
  onChange: (action: RuleAction) => void
  onDelete: () => void
  readOnly?: boolean
}

const ACTION_CONFIGS: Record<ActionType, {
  label: string
  icon: string
  color: string
  valueType: 'decision' | 'risk' | 'traffic_light' | 'text' | 'deviation' | 'score'
  description: string
}> = {
  SET_DECISION: {
    label: 'Set Decision',
    icon: '⚖️',
    color: 'bg-red-50 border-red-200',
    valueType: 'decision',
    description: 'Sets the final loan decision',
  },
  SET_RISK: {
    label: 'Set Risk Level',
    icon: '⚠️',
    color: 'bg-yellow-50 border-yellow-200',
    valueType: 'risk',
    description: 'Sets the risk category',
  },
  SET_TRAFFIC_LIGHT: {
    label: 'Set Traffic Light',
    icon: '🚦',
    color: 'bg-green-50 border-green-200',
    valueType: 'traffic_light',
    description: 'Sets the visual status indicator',
  },
  ADD_DEVIATION: {
    label: 'Add Deviation',
    icon: '📋',
    color: 'bg-orange-50 border-orange-200',
    valueType: 'deviation',
    description: 'Flags a policy deviation',
  },
  SET_FIELD: {
    label: 'Set Output Field',
    icon: '📝',
    color: 'bg-blue-50 border-blue-200',
    valueType: 'text',
    description: 'Sets a custom output field value',
  },
  ADD_TAG: {
    label: 'Add Tag',
    icon: '🏷️',
    color: 'bg-purple-50 border-purple-200',
    valueType: 'text',
    description: 'Adds a tag to the decision',
  },
  SET_SCORE: {
    label: 'Set Risk Score',
    icon: '📊',
    color: 'bg-teal-50 border-teal-200',
    valueType: 'score',
    description: 'Sets or adjusts the risk score (0-100)',
  },
}

export function ActionEditor({ action, index, onChange, onDelete, readOnly }: Props) {
  const config = ACTION_CONFIGS[action.type]

  return (
    <div className={`flex items-center gap-3 rounded-xl border-2 p-3 ${config.color}`}>
      {/* Action Number */}
      <div className="w-6 h-6 rounded-full bg-white border border-gray-200 flex items-center justify-center text-xs font-bold text-gray-500">
        {index + 1}
      </div>

      {/* Icon */}
      <span className="text-xl" role="img">{config.icon}</span>

      {/* Type Selector */}
      <select
        disabled={readOnly}
        value={action.type}
        onChange={(e) => onChange({ ...action, type: e.target.value as ActionType, value: '' })}
        className="text-sm font-medium border border-transparent bg-white/70 rounded-lg px-2 py-1 focus:outline-none focus:border-blue-400"
      >
        {Object.entries(ACTION_CONFIGS).map(([type, cfg]) => (
          <option key={type} value={type}>{cfg.label}</option>
        ))}
      </select>

      {/* Value Input */}
      {config.valueType === 'decision' && (
        <select
          disabled={readOnly}
          value={action.value ?? 'REJECT'}
          onChange={(e) => onChange({ ...action, value: e.target.value })}
          className="text-sm font-semibold border border-white/70 bg-white rounded-lg px-3 py-1 focus:outline-none focus:border-blue-400"
        >
          {(['APPROVE', 'REJECT', 'DEVIATION', 'REFER'] as Decision[]).map((d) => (
            <option key={d} value={d}>{d}</option>
          ))}
        </select>
      )}

      {config.valueType === 'risk' && (
        <select
          disabled={readOnly}
          value={action.value ?? 'HIGH'}
          onChange={(e) => onChange({ ...action, value: e.target.value })}
          className="text-sm font-semibold border border-white/70 bg-white rounded-lg px-3 py-1 focus:outline-none focus:border-blue-400"
        >
          {(['LOW', 'MEDIUM', 'HIGH', 'CRITICAL'] as RiskCategory[]).map((r) => (
            <option key={r} value={r}>{r}</option>
          ))}
        </select>
      )}

      {config.valueType === 'traffic_light' && (
        <select
          disabled={readOnly}
          value={action.value ?? 'RED'}
          onChange={(e) => onChange({ ...action, value: e.target.value })}
          className="text-sm font-semibold border border-white/70 bg-white rounded-lg px-3 py-1 focus:outline-none focus:border-blue-400"
        >
          {(['GREEN', 'AMBER', 'RED'] as TrafficLight[]).map((t) => (
            <option key={t} value={t}>{t}</option>
          ))}
        </select>
      )}

      {config.valueType === 'deviation' && (
        <div className="flex gap-2 flex-1">
          <input
            disabled={readOnly}
            value={action.value ?? ''}
            onChange={(e) => onChange({ ...action, value: e.target.value })}
            placeholder="Deviation code (e.g., LOW_BUREAU_SCORE)"
            className="flex-1 text-sm border border-white/70 bg-white rounded-lg px-3 py-1 focus:outline-none focus:border-blue-400 font-mono"
          />
          <input
            disabled={readOnly}
            value={action.parameters?.severity ?? 'HIGH'}
            onChange={(e) => onChange({ ...action, parameters: { ...action.parameters, severity: e.target.value } })}
            placeholder="Severity"
            className="w-24 text-sm border border-white/70 bg-white rounded-lg px-2 py-1 focus:outline-none focus:border-blue-400"
          />
        </div>
      )}

      {config.valueType === 'text' && (
        <div className="flex gap-2 flex-1">
          {action.type === 'SET_FIELD' && (
            <input
              disabled={readOnly}
              value={action.field ?? ''}
              onChange={(e) => onChange({ ...action, field: e.target.value })}
              placeholder="Field name"
              className="w-40 text-sm border border-white/70 bg-white rounded-lg px-2 py-1 focus:outline-none focus:border-blue-400 font-mono"
            />
          )}
          <input
            disabled={readOnly}
            value={action.value ?? ''}
            onChange={(e) => onChange({ ...action, value: e.target.value })}
            placeholder={action.type === 'ADD_TAG' ? 'tag-name' : 'Value'}
            className="flex-1 text-sm border border-white/70 bg-white rounded-lg px-3 py-1 focus:outline-none focus:border-blue-400"
          />
        </div>
      )}

      {config.valueType === 'score' && (
        <div className="flex items-center gap-2">
          <input
            disabled={readOnly}
            type="number"
            min={-100}
            max={100}
            value={action.value ?? ''}
            onChange={(e) => onChange({ ...action, value: e.target.value })}
            placeholder="0–100 or +/-offset"
            className="w-40 text-sm border border-white/70 bg-white rounded-lg px-3 py-1 focus:outline-none focus:border-blue-400"
          />
          <span className="text-xs text-gray-500">Prefix +/- for adjustment</span>
        </div>
      )}

      {/* Description */}
      <span className="text-xs text-gray-500 hidden xl:block">{config.description}</span>

      {/* Delete */}
      {!readOnly && (
        <button
          onClick={onDelete}
          className="ml-auto text-gray-300 hover:text-red-500 transition-colors text-lg"
          title="Delete action"
        >
          ✕
        </button>
      )}
    </div>
  )
}
