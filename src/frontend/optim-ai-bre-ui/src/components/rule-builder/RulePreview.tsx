'use client'

import React from 'react'
import type { RuleDefinition, ConditionGroup, ConditionNode, RuleAction, FieldCatalogEntry } from '@/types'

interface Props {
  definition: RuleDefinition
  fieldCatalog: FieldCatalogEntry[]
}

const OPERATOR_LABELS: Record<string, string> = {
  EQUALS: '=',
  NOT_EQUALS: '≠',
  GREATER_THAN: '>',
  GREATER_THAN_OR_EQUAL: '≥',
  LESS_THAN: '<',
  LESS_THAN_OR_EQUAL: '≤',
  BETWEEN: 'between',
  IN: 'in',
  NOT_IN: 'not in',
  CONTAINS: 'contains',
  IS_NULL: 'is empty',
  IS_NOT_NULL: 'is not empty',
  IS_TRUE: 'is true',
  IS_FALSE: 'is false',
  REGEX: 'matches',
}

const ACTION_COLORS: Record<string, string> = {
  SET_DECISION: 'bg-red-100 text-red-800',
  SET_RISK: 'bg-yellow-100 text-yellow-800',
  SET_TRAFFIC_LIGHT: 'bg-green-100 text-green-800',
  ADD_DEVIATION: 'bg-orange-100 text-orange-800',
  SET_FIELD: 'bg-blue-100 text-blue-800',
  ADD_TAG: 'bg-purple-100 text-purple-800',
  SET_SCORE: 'bg-teal-100 text-teal-800',
}

export function RulePreview({ definition, fieldCatalog }: Props) {
  const getFieldName = (path: string) =>
    fieldCatalog.find((f) => f.fieldPath === path)?.displayName ?? path

  const renderConditionGroup = (group: ConditionGroup, depth = 0): React.ReactNode => {
    const indent = depth * 24

    return (
      <div style={{ marginLeft: indent }} className="space-y-1">
        {group.rules.map((node, idx) => (
          <div key={node.id}>
            {idx > 0 && (
              <div className="flex items-center gap-2 my-1">
                <div className="w-4 h-px bg-gray-300" />
                <span className={`px-2 py-0.5 rounded text-xs font-bold ${
                  group.operator === 'AND' ? 'bg-blue-100 text-blue-700' :
                  group.operator === 'OR' ? 'bg-purple-100 text-purple-700' :
                  'bg-red-100 text-red-700'
                }`}>
                  {group.operator}
                </span>
              </div>
            )}
            {renderNode(node, depth)}
          </div>
        ))}
      </div>
    )
  }

  const renderNode = (node: ConditionNode, depth: number): React.ReactNode => {
    if (node.isGroup && node.group) {
      return (
        <div className="border-l-2 border-gray-200 pl-3 py-1">
          <span className="text-xs text-gray-400 mb-1 block">{node.group.operator} group:</span>
          {renderConditionGroup(node.group, depth + 1)}
        </div>
      )
    }

    return (
      <div className="flex items-center gap-2 font-mono text-sm bg-gray-50 rounded-lg px-3 py-2 border border-gray-100">
        <span className="text-blue-700 font-semibold">{getFieldName(node.field ?? '')}</span>
        <span className="text-gray-500">{OPERATOR_LABELS[node.operator ?? ''] ?? node.operator}</span>
        {node.value !== undefined && node.value !== '' && (
          <span className="text-emerald-700 font-semibold">
            {node.operator === 'BETWEEN'
              ? `${node.value} and ${node.value2}`
              : String(node.value)}
          </span>
        )}
      </div>
    )
  }

  const renderAction = (action: RuleAction): string => {
    const colorClass = ACTION_COLORS[action.type] ?? 'bg-gray-100 text-gray-800'
    switch (action.type) {
      case 'SET_DECISION': return `Decision → ${action.value}`
      case 'SET_RISK': return `Risk Level → ${action.value}`
      case 'SET_TRAFFIC_LIGHT': return `Traffic Light → ${action.value}`
      case 'ADD_DEVIATION': return `Deviation: ${action.value} (${action.parameters?.severity ?? 'MEDIUM'})`
      case 'SET_FIELD': return `Set ${action.field} = ${action.value}`
      case 'ADD_TAG': return `Tag: ${action.value}`
      case 'SET_SCORE': return `Risk Score ${action.value?.startsWith?.('+') || action.value?.startsWith?.('-') ? 'adjust' : '='} ${action.value}`
      default: return `${action.type}: ${action.value}`
    }
  }

  const hasConditions = definition.conditions.rules.length > 0
  const hasActions = definition.actions.length > 0

  return (
    <div className="max-w-3xl mx-auto">
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="bg-gradient-to-r from-blue-600 to-blue-700 px-6 py-4">
          <h2 className="text-white font-semibold text-lg">Rule Preview</h2>
          <p className="text-blue-200 text-sm">Human-readable rule representation</p>
        </div>

        <div className="p-6 font-mono">
          {/* IF block */}
          <div className="mb-6">
            <div className="flex items-center gap-3 mb-3">
              <span className="px-3 py-1 bg-blue-600 text-white rounded-lg text-sm font-bold">IF</span>
              <span className="text-gray-400 text-sm">all/any of these conditions match:</span>
            </div>
            {hasConditions ? (
              renderConditionGroup(definition.conditions)
            ) : (
              <p className="text-gray-400 text-sm italic ml-4">No conditions defined</p>
            )}
          </div>

          {/* Divider */}
          <div className="border-t-2 border-dashed border-gray-200 my-4" />

          {/* THEN block */}
          <div>
            <div className="flex items-center gap-3 mb-3">
              <span className="px-3 py-1 bg-emerald-600 text-white rounded-lg text-sm font-bold">THEN</span>
              <span className="text-gray-400 text-sm">execute these actions:</span>
            </div>
            {hasActions ? (
              <div className="space-y-2 ml-4">
                {definition.actions.map((action, idx) => (
                  <div key={action.id} className="flex items-center gap-3">
                    <span className="text-xs text-gray-400">{idx + 1}.</span>
                    <span className={`px-2 py-1 rounded-lg text-xs font-semibold ${ACTION_COLORS[action.type] ?? 'bg-gray-100'}`}>
                      {renderAction(action)}
                    </span>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-gray-400 text-sm italic ml-4">No actions defined</p>
            )}
          </div>

          {/* Metadata */}
          {definition.metadata && (
            <>
              <div className="border-t border-gray-100 mt-6 pt-4">
                <div className="flex gap-4 text-xs text-gray-400">
                  <span>Order: {definition.metadata.executionOrder ?? 0}</span>
                  <span>Error: {definition.metadata.errorHandling ?? 'SKIP'}</span>
                  {definition.metadata.stopOnMatch && (
                    <span className="text-amber-600 font-medium">⚡ Stop on Match</span>
                  )}
                </div>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
