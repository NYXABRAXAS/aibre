'use client'

import React from 'react'
import { v4 as uuidv4 } from 'uuid'
import type { ConditionGroup, ConditionNode, FieldCatalogEntry, LogicalOperator, ComparisonOperator } from '@/types'
import { ConditionNodeEditor } from './ConditionNodeEditor'

interface Props {
  group: ConditionGroup
  fieldCatalog: FieldCatalogEntry[]
  onChange: (group: ConditionGroup) => void
  readOnly?: boolean
  depth: number
}

const GROUP_COLORS = [
  'border-blue-300 bg-blue-50',
  'border-purple-300 bg-purple-50',
  'border-teal-300 bg-teal-50',
  'border-orange-300 bg-orange-50',
]

export function ConditionGroupEditor({ group, fieldCatalog, onChange, readOnly, depth }: Props) {
  const colorClass = GROUP_COLORS[depth % GROUP_COLORS.length]

  const updateOperator = (operator: LogicalOperator) =>
    onChange({ ...group, operator })

  const addCondition = () => {
    const newNode: ConditionNode = {
      id: uuidv4(),
      isGroup: false,
      field: '',
      operator: 'EQUALS',
      value: '',
      valueType: 'LITERAL',
    }
    onChange({ ...group, rules: [...group.rules, newNode] })
  }

  const addGroup = () => {
    const nestedGroup: ConditionGroup = {
      id: uuidv4(),
      operator: 'AND',
      rules: [],
    }
    const newNode: ConditionNode = {
      id: uuidv4(),
      isGroup: true,
      group: nestedGroup,
    }
    onChange({ ...group, rules: [...group.rules, newNode] })
  }

  const updateNode = (index: number, updated: ConditionNode) => {
    const next = [...group.rules]
    next[index] = updated
    onChange({ ...group, rules: next })
  }

  const deleteNode = (index: number) => {
    onChange({ ...group, rules: group.rules.filter((_, i) => i !== index) })
  }

  return (
    <div className={`rounded-lg border-2 ${colorClass} p-4`}>
      {/* Group Header */}
      <div className="flex items-center gap-3 mb-4">
        <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">
          Match
        </span>
        <div className="flex rounded-lg overflow-hidden border border-gray-200">
          {(['AND', 'OR', 'NOT'] as LogicalOperator[]).map((op) => (
            <button
              key={op}
              disabled={readOnly}
              onClick={() => updateOperator(op)}
              className={`px-3 py-1 text-xs font-bold transition-colors ${
                group.operator === op
                  ? op === 'AND'
                    ? 'bg-blue-600 text-white'
                    : op === 'OR'
                    ? 'bg-purple-600 text-white'
                    : 'bg-red-600 text-white'
                  : 'bg-white text-gray-600 hover:bg-gray-50'
              }`}
            >
              {op}
            </button>
          ))}
        </div>
        <span className="text-xs text-gray-400">
          {group.operator === 'AND' ? 'all of the following' : group.operator === 'OR' ? 'any of the following' : 'NOT the following'}
        </span>
      </div>

      {/* Conditions */}
      <div className="space-y-2">
        {group.rules.length === 0 && (
          <div className="text-center py-4 text-gray-400 text-sm">
            {readOnly ? 'No conditions' : 'Add conditions below'}
          </div>
        )}
        {group.rules.map((node, idx) => (
          <div key={node.id} className="flex items-start gap-2">
            {idx > 0 && (
              <div className="mt-3 px-2 py-0.5 rounded text-xs font-bold text-gray-400 min-w-[36px] text-center">
                {group.operator}
              </div>
            )}
            <div className={`flex-1 ${idx === 0 ? '' : ''}`}>
              {node.isGroup && node.group ? (
                <ConditionGroupEditor
                  group={node.group}
                  fieldCatalog={fieldCatalog}
                  depth={depth + 1}
                  readOnly={readOnly}
                  onChange={(updated) =>
                    updateNode(idx, { ...node, group: updated })
                  }
                />
              ) : (
                <ConditionNodeEditor
                  node={node}
                  fieldCatalog={fieldCatalog}
                  readOnly={readOnly}
                  onChange={(updated) => updateNode(idx, updated)}
                  onDelete={() => deleteNode(idx)}
                />
              )}
            </div>
            {!readOnly && (
              <button
                onClick={() => deleteNode(idx)}
                className="mt-2 text-gray-300 hover:text-red-500 transition-colors"
                title="Delete condition"
              >
                ✕
              </button>
            )}
          </div>
        ))}
      </div>

      {/* Add Buttons */}
      {!readOnly && (
        <div className="flex items-center gap-2 mt-4">
          <button
            onClick={addCondition}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-blue-700 bg-blue-100 hover:bg-blue-200 rounded-lg transition-colors"
          >
            <span className="text-base leading-none">+</span> Add Condition
          </button>
          {depth < 3 && (
            <button
              onClick={addGroup}
              className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-purple-700 bg-purple-100 hover:bg-purple-200 rounded-lg transition-colors"
            >
              <span className="text-base leading-none">⊞</span> Add Group
            </button>
          )}
        </div>
      )}
    </div>
  )
}
