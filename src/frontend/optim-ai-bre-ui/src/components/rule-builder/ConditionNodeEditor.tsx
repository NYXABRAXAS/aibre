'use client'

import React, { useState } from 'react'
import type { ConditionNode, FieldCatalogEntry, ComparisonOperator } from '@/types'

interface Props {
  node: ConditionNode
  fieldCatalog: FieldCatalogEntry[]
  onChange: (node: ConditionNode) => void
  onDelete: () => void
  readOnly?: boolean
}

const OPERATORS: { value: ComparisonOperator; label: string; showValue: 'single' | 'double' | 'none' | 'multi' }[] = [
  { value: 'EQUALS', label: '= Equals', showValue: 'single' },
  { value: 'NOT_EQUALS', label: '≠ Not Equals', showValue: 'single' },
  { value: 'GREATER_THAN', label: '> Greater Than', showValue: 'single' },
  { value: 'GREATER_THAN_OR_EQUAL', label: '≥ Greater Than or Equal', showValue: 'single' },
  { value: 'LESS_THAN', label: '< Less Than', showValue: 'single' },
  { value: 'LESS_THAN_OR_EQUAL', label: '≤ Less Than or Equal', showValue: 'single' },
  { value: 'BETWEEN', label: '↔ Between', showValue: 'double' },
  { value: 'IN', label: '∈ In (comma separated)', showValue: 'single' },
  { value: 'NOT_IN', label: '∉ Not In', showValue: 'single' },
  { value: 'CONTAINS', label: '⊆ Contains', showValue: 'single' },
  { value: 'IS_NULL', label: '∅ Is Empty/Null', showValue: 'none' },
  { value: 'IS_NOT_NULL', label: '• Is Not Null', showValue: 'none' },
  { value: 'IS_TRUE', label: '✓ Is True', showValue: 'none' },
  { value: 'IS_FALSE', label: '✗ Is False', showValue: 'none' },
  { value: 'REGEX', label: '⚙ Matches Regex', showValue: 'single' },
]

const FIELD_CATEGORIES = ['BUREAU', 'INCOME', 'PERSONAL', 'EMPLOYMENT', 'VEHICLE', 'FI', 'LOAN', 'RATIOS', 'FRAUD', 'GST', 'ITR', 'KYC']

export function ConditionNodeEditor({ node, fieldCatalog, onChange, onDelete, readOnly }: Props) {
  const [showFieldSearch, setShowFieldSearch] = useState(false)
  const [fieldSearch, setFieldSearch] = useState('')

  const selectedOp = OPERATORS.find((o) => o.value === node.operator)
  const selectedField = fieldCatalog.find((f) => f.fieldPath === node.field)

  const filteredFields = fieldCatalog.filter(
    (f) =>
      !fieldSearch ||
      f.fieldPath.toLowerCase().includes(fieldSearch.toLowerCase()) ||
      f.displayName.toLowerCase().includes(fieldSearch.toLowerCase())
  )

  const groupedFields = FIELD_CATEGORIES.reduce<Record<string, FieldCatalogEntry[]>>((acc, cat) => {
    const fields = filteredFields.filter((f) => f.category === cat)
    if (fields.length > 0) acc[cat] = fields
    return acc
  }, {})

  return (
    <div className="flex items-center gap-2 bg-white border border-gray-200 rounded-lg px-3 py-2 group hover:border-blue-300 transition-colors">
      {/* Field Selector */}
      <div className="relative min-w-[220px]">
        <button
          disabled={readOnly}
          onClick={() => !readOnly && setShowFieldSearch(!showFieldSearch)}
          className="w-full text-left text-sm px-2 py-1 rounded border border-gray-200 hover:border-blue-400 focus:outline-none focus:border-blue-500 bg-gray-50"
        >
          {selectedField ? (
            <span className="text-gray-800">{selectedField.displayName}</span>
          ) : (
            <span className="text-gray-400">Select field...</span>
          )}
        </button>

        {showFieldSearch && !readOnly && (
          <div className="absolute top-full left-0 mt-1 w-80 bg-white border border-gray-200 rounded-xl shadow-xl z-50 max-h-72 overflow-auto">
            <div className="p-2 border-b sticky top-0 bg-white">
              <input
                autoFocus
                value={fieldSearch}
                onChange={(e) => setFieldSearch(e.target.value)}
                placeholder="Search fields..."
                className="w-full text-sm px-2 py-1.5 border border-gray-200 rounded-lg focus:outline-none focus:border-blue-400"
              />
            </div>
            {Object.entries(groupedFields).map(([cat, fields]) => (
              <div key={cat}>
                <div className="px-3 py-1 text-xs font-semibold text-gray-400 uppercase tracking-wider bg-gray-50">
                  {cat}
                </div>
                {fields.map((f) => (
                  <button
                    key={f.fieldPath}
                    onClick={() => {
                      onChange({ ...node, field: f.fieldPath })
                      setShowFieldSearch(false)
                      setFieldSearch('')
                    }}
                    className="w-full text-left px-3 py-2 text-sm hover:bg-blue-50 hover:text-blue-700 flex items-center justify-between"
                  >
                    <span>{f.displayName}</span>
                    <span className="text-xs text-gray-400 font-mono">{f.dataType}</span>
                  </button>
                ))}
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Operator Selector */}
      <select
        disabled={readOnly}
        value={node.operator ?? 'EQUALS'}
        onChange={(e) => onChange({ ...node, operator: e.target.value as ComparisonOperator })}
        className="text-sm border border-gray-200 rounded-lg px-2 py-1 bg-gray-50 focus:outline-none focus:border-blue-500 min-w-[160px]"
      >
        {OPERATORS.map((op) => (
          <option key={op.value} value={op.value}>{op.label}</option>
        ))}
      </select>

      {/* Value Input(s) */}
      {selectedOp?.showValue === 'single' && (
        <ValueInput
          value={String(node.value ?? '')}
          fieldType={selectedField?.dataType}
          readOnly={readOnly}
          onChange={(v) => onChange({ ...node, value: v })}
          placeholder="Value"
        />
      )}

      {selectedOp?.showValue === 'double' && (
        <>
          <ValueInput
            value={String(node.value ?? '')}
            fieldType={selectedField?.dataType}
            readOnly={readOnly}
            onChange={(v) => onChange({ ...node, value: v })}
            placeholder="From"
          />
          <span className="text-gray-400 text-sm">and</span>
          <ValueInput
            value={String(node.value2 ?? '')}
            fieldType={selectedField?.dataType}
            readOnly={readOnly}
            onChange={(v) => onChange({ ...node, value2: v })}
            placeholder="To"
          />
        </>
      )}

      {/* Field badge */}
      {node.field && (
        <span className="text-xs font-mono text-gray-400 truncate max-w-[120px]" title={node.field}>
          {node.field}
        </span>
      )}
    </div>
  )
}

function ValueInput({
  value,
  fieldType,
  onChange,
  placeholder,
  readOnly,
}: {
  value: string
  fieldType?: string
  onChange: (v: string) => void
  placeholder: string
  readOnly?: boolean
}) {
  if (fieldType === 'BOOLEAN') {
    return (
      <select
        disabled={readOnly}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="text-sm border border-gray-200 rounded-lg px-2 py-1 bg-gray-50 focus:outline-none focus:border-blue-500"
      >
        <option value="">Select...</option>
        <option value="true">True</option>
        <option value="false">False</option>
      </select>
    )
  }

  return (
    <input
      type={fieldType === 'NUMBER' ? 'number' : fieldType === 'DATE' ? 'date' : 'text'}
      disabled={readOnly}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
      className="text-sm border border-gray-200 rounded-lg px-2 py-1.5 bg-gray-50 focus:outline-none focus:border-blue-500 w-32"
    />
  )
}
