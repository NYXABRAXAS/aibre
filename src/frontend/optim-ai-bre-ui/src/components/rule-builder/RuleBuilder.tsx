'use client'

import React, { useState, useCallback } from 'react'
import { v4 as uuidv4 } from 'uuid'
import {
  DndContext,
  DragEndEvent,
  closestCenter,
  PointerSensor,
  useSensor,
  useSensors,
} from '@dnd-kit/core'
import { SortableContext, verticalListSortingStrategy } from '@dnd-kit/sortable'
import type {
  ConditionGroup,
  ConditionNode,
  RuleAction,
  RuleDefinition,
  LogicalOperator,
  ComparisonOperator,
  ActionType,
  FieldCatalogEntry,
} from '@/types'
import { ConditionGroupEditor } from './ConditionGroupEditor'
import { ActionEditor } from './ActionEditor'
import { RulePreview } from './RulePreview'

interface RuleBuilderProps {
  initialDefinition?: RuleDefinition
  fieldCatalog: FieldCatalogEntry[]
  onChange: (definition: RuleDefinition) => void
  readOnly?: boolean
}

const defaultConditionGroup = (): ConditionGroup => ({
  id: uuidv4(),
  operator: 'AND',
  rules: [],
})

const defaultDefinition = (): RuleDefinition => ({
  conditions: defaultConditionGroup(),
  actions: [],
  metadata: { executionOrder: 0, stopOnMatch: false, errorHandling: 'SKIP' },
})

export function RuleBuilder({
  initialDefinition,
  fieldCatalog,
  onChange,
  readOnly = false,
}: RuleBuilderProps) {
  const [definition, setDefinition] = useState<RuleDefinition>(
    initialDefinition ?? defaultDefinition()
  )
  const [activeTab, setActiveTab] = useState<'builder' | 'preview' | 'json'>('builder')

  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 8 } }))

  const updateDefinition = useCallback(
    (updater: (prev: RuleDefinition) => RuleDefinition) => {
      setDefinition((prev) => {
        const next = updater(prev)
        onChange(next)
        return next
      })
    },
    [onChange]
  )

  const handleConditionsChange = useCallback(
    (conditions: ConditionGroup) => {
      updateDefinition((prev) => ({ ...prev, conditions }))
    },
    [updateDefinition]
  )

  const handleActionsChange = useCallback(
    (actions: RuleAction[]) => {
      updateDefinition((prev) => ({ ...prev, actions }))
    },
    [updateDefinition]
  )

  const addAction = () => {
    const newAction: RuleAction = {
      id: uuidv4(),
      type: 'SET_DECISION',
      value: 'REJECT',
    }
    handleActionsChange([...definition.actions, newAction])
  }

  return (
    <div className="flex flex-col h-full bg-gray-50">
      {/* Tabs */}
      <div className="flex border-b border-gray-200 bg-white px-6">
        {(['builder', 'preview', 'json'] as const).map((tab) => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            className={`py-3 px-6 text-sm font-medium capitalize border-b-2 transition-colors ${
              activeTab === tab
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            {tab === 'json' ? 'JSON View' : tab.charAt(0).toUpperCase() + tab.slice(1)}
          </button>
        ))}
      </div>

      {/* Content */}
      <div className="flex-1 overflow-auto p-6">
        {activeTab === 'builder' && (
          <DndContext sensors={sensors} collisionDetection={closestCenter}>
            <div className="max-w-4xl mx-auto space-y-6">
              {/* CONDITIONS */}
              <section className="bg-white rounded-xl shadow-sm border border-gray-200">
                <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-lg bg-blue-100 flex items-center justify-center">
                      <span className="text-blue-600 text-sm font-bold">IF</span>
                    </div>
                    <h2 className="text-base font-semibold text-gray-900">Conditions</h2>
                    <span className="text-xs text-gray-400">
                      Define when this rule should trigger
                    </span>
                  </div>
                </div>
                <div className="p-6">
                  <ConditionGroupEditor
                    group={definition.conditions}
                    fieldCatalog={fieldCatalog}
                    onChange={handleConditionsChange}
                    readOnly={readOnly}
                    depth={0}
                  />
                </div>
              </section>

              {/* ACTIONS */}
              <section className="bg-white rounded-xl shadow-sm border border-gray-200">
                <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-lg bg-emerald-100 flex items-center justify-center">
                      <span className="text-emerald-600 text-sm font-bold">THEN</span>
                    </div>
                    <h2 className="text-base font-semibold text-gray-900">Actions</h2>
                    <span className="text-xs text-gray-400">What happens when conditions match</span>
                  </div>
                  {!readOnly && (
                    <button
                      onClick={addAction}
                      className="flex items-center gap-1 text-sm text-blue-600 hover:text-blue-700 font-medium"
                    >
                      <span className="text-lg leading-none">+</span> Add Action
                    </button>
                  )}
                </div>
                <div className="p-6 space-y-3">
                  {definition.actions.length === 0 ? (
                    <div className="text-center py-8 text-gray-400">
                      <p className="text-sm">No actions defined yet.</p>
                      {!readOnly && (
                        <button
                          onClick={addAction}
                          className="mt-2 text-sm text-blue-600 hover:text-blue-700"
                        >
                          + Add your first action
                        </button>
                      )}
                    </div>
                  ) : (
                    definition.actions.map((action, idx) => (
                      <ActionEditor
                        key={action.id}
                        action={action}
                        index={idx}
                        readOnly={readOnly}
                        onChange={(updated) => {
                          const next = [...definition.actions]
                          next[idx] = updated
                          handleActionsChange(next)
                        }}
                        onDelete={() => {
                          handleActionsChange(definition.actions.filter((_, i) => i !== idx))
                        }}
                      />
                    ))
                  )}
                </div>
              </section>

              {/* METADATA */}
              <section className="bg-white rounded-xl shadow-sm border border-gray-200">
                <div className="px-6 py-4 border-b border-gray-100">
                  <h2 className="text-base font-semibold text-gray-900">Rule Settings</h2>
                </div>
                <div className="p-6 grid grid-cols-3 gap-4">
                  <label className="flex flex-col gap-1">
                    <span className="text-xs font-medium text-gray-600">Execution Order</span>
                    <input
                      type="number"
                      value={definition.metadata?.executionOrder ?? 0}
                      disabled={readOnly}
                      onChange={(e) =>
                        updateDefinition((prev) => ({
                          ...prev,
                          metadata: { ...prev.metadata, executionOrder: Number(e.target.value) },
                        }))
                      }
                      className="border border-gray-200 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                    />
                  </label>
                  <label className="flex flex-col gap-1">
                    <span className="text-xs font-medium text-gray-600">Error Handling</span>
                    <select
                      value={definition.metadata?.errorHandling ?? 'SKIP'}
                      disabled={readOnly}
                      onChange={(e) =>
                        updateDefinition((prev) => ({
                          ...prev,
                          metadata: {
                            ...prev.metadata,
                            errorHandling: e.target.value as 'SKIP' | 'FAIL' | 'USE_DEFAULT',
                          },
                        }))
                      }
                      className="border border-gray-200 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500"
                    >
                      <option value="SKIP">Skip on Error</option>
                      <option value="FAIL">Fail on Error</option>
                      <option value="USE_DEFAULT">Use Default</option>
                    </select>
                  </label>
                  <label className="flex items-center gap-3 mt-5">
                    <input
                      type="checkbox"
                      checked={definition.metadata?.stopOnMatch ?? false}
                      disabled={readOnly}
                      onChange={(e) =>
                        updateDefinition((prev) => ({
                          ...prev,
                          metadata: { ...prev.metadata, stopOnMatch: e.target.checked },
                        }))
                      }
                      className="w-4 h-4 rounded border-gray-300 text-blue-600"
                    />
                    <span className="text-sm font-medium text-gray-700">Stop on Match</span>
                  </label>
                </div>
              </section>
            </div>
          </DndContext>
        )}

        {activeTab === 'preview' && (
          <RulePreview definition={definition} fieldCatalog={fieldCatalog} />
        )}

        {activeTab === 'json' && (
          <div className="max-w-4xl mx-auto">
            <div className="bg-gray-900 rounded-xl p-6 overflow-auto">
              <pre className="text-green-400 text-sm font-mono whitespace-pre-wrap">
                {JSON.stringify(definition, null, 2)}
              </pre>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
