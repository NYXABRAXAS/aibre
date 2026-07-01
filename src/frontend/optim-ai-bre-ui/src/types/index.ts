// ============================================================
// OPTIM AI BRE ENGINE - TypeScript Types
// ============================================================

export type Decision = 'APPROVE' | 'REJECT' | 'DEVIATION' | 'REFER' | 'PENDING'
export type TrafficLight = 'GREEN' | 'AMBER' | 'RED'
export type RiskCategory = 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL'
export type RuleStatus = 'DRAFT' | 'PENDING_APPROVAL' | 'APPROVED' | 'PUBLISHED' | 'ARCHIVED'
export type RuleType = 'ELIGIBILITY' | 'CREDIT' | 'BUREAU' | 'FI' | 'VALUATION' | 'FRAUD' | 'COMPLIANCE' | 'DEVIATION' | 'INCOME' | 'KYC' | 'VEHICLE' | 'GUARANTOR'
export type LogicalOperator = 'AND' | 'OR' | 'NOT'
export type ComparisonOperator = 'EQUALS' | 'NOT_EQUALS' | 'GREATER_THAN' | 'GREATER_THAN_OR_EQUAL' | 'LESS_THAN' | 'LESS_THAN_OR_EQUAL' | 'BETWEEN' | 'NOT_BETWEEN' | 'IN' | 'NOT_IN' | 'CONTAINS' | 'IS_NULL' | 'IS_NOT_NULL' | 'IS_TRUE' | 'IS_FALSE' | 'REGEX'
export type ActionType = 'SET_DECISION' | 'SET_RISK' | 'SET_TRAFFIC_LIGHT' | 'ADD_DEVIATION' | 'SET_FIELD' | 'ADD_TAG' | 'SET_SCORE'
export type Severity = 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL'
export type ScopeType = 'PRODUCT' | 'BRANCH' | 'STAGE' | 'USER_ROLE' | 'GLOBAL'

// ============================================================
// RULE ENGINE
// ============================================================

export interface ConditionGroup {
  id: string
  operator: LogicalOperator
  rules: ConditionNode[]
}

export interface ConditionNode {
  id: string
  isGroup: boolean
  // Leaf condition
  field?: string
  operator?: ComparisonOperator
  value?: string | number | boolean | string[]
  value2?: string | number  // for BETWEEN
  valueType?: 'LITERAL' | 'FIELD' | 'FUNCTION'
  referenceField?: string
  // Group
  group?: ConditionGroup
}

export interface RuleAction {
  id: string
  type: ActionType
  value?: string
  field?: string
  parameters?: Record<string, string>
}

export interface RuleDefinition {
  conditions: ConditionGroup
  actions: RuleAction[]
  metadata?: {
    executionOrder?: number
    stopOnMatch?: boolean
    errorHandling?: 'SKIP' | 'FAIL' | 'USE_DEFAULT'
    notes?: string
  }
}

export interface RuleScope {
  scopeType: ScopeType
  scopeValue: string
  isExcluded: boolean
}

export interface RuleVersion {
  id: string
  versionNumber: number
  versionLabel: string
  ruleDefinition: RuleDefinition
  status: RuleStatus
  changeSummary?: string
  isCurrent: boolean
  createdAt: string
  approvedAt?: string
  publishedAt?: string
}

export interface Rule {
  id: string
  ruleCode: string
  ruleName: string
  description?: string
  categoryId?: string
  ruleType: RuleType
  priority: number
  isActive: boolean
  isPublished: boolean
  status: RuleStatus
  tags: string[]
  currentVersion?: RuleVersion
  scopes: RuleScope[]
  versionCount?: number
  createdAt: string
  updatedAt: string
}

export interface RuleCategory {
  id: string
  categoryCode: string
  categoryName: string
  description?: string
  icon?: string
  sortOrder: number
  isActive: boolean
}

export interface FieldCatalogEntry {
  fieldPath: string
  displayName: string
  dataType: 'NUMBER' | 'STRING' | 'BOOLEAN' | 'DATE' | 'ARRAY' | 'OBJECT'
  category?: string
  description?: string
}

// ============================================================
// EXECUTION
// ============================================================

export interface BREDecisionResponse {
  requestId: string
  correlationId: string
  applicationId?: string
  decision: Decision
  trafficLight: TrafficLight
  riskScore: number
  riskCategory: RiskCategory
  totalRulesEvaluated: number
  rulesPassed: number
  rulesFailed: number
  deviationsCount: number
  executionMs: number
  ruleResults: RuleResult[]
  aiSummary?: string
  aiAnalysis?: AiAnalysis
  reportId?: string
  generatedAt: string
}

export interface RuleResult {
  ruleCode: string
  ruleName: string
  isMatched: boolean
  actionsExecuted: string[]
  executionMs: number
}

export interface AiAnalysis {
  riskSummary: string
  creditSummary: string
  strengths: string[]
  weaknesses: string[]
  deviationsSummary: string
  approvalRecommendation: string
  rejectionReasons: string[]
  additionalDocuments: string[]
  underwritingNotes: string
  confidenceScore: number
}

export interface Deviation {
  deviationCode: string
  deviationName: string
  severity: Severity
  reason: string
  fieldPath?: string
  actualValue?: string
  expectedValue?: string
  recommendedAction?: string
  isOverridden: boolean
}

// ============================================================
// DASHBOARD
// ============================================================

export interface DashboardStats {
  totalRules: number
  activeRules: number
  publishedRules: number
  draftRules: number
  totalExecutions: number
  todayExecutions: number
  approvalRate: number
  rejectionRate: number
  deviationRate: number
  avgRiskScore: number
  avgExecutionMs: number
}

export interface RuleHitAnalysis {
  ruleCode: string
  ruleName: string
  totalEvaluations: number
  matchedCount: number
  hitRate: number
  avgExecutionMs: number
}

export interface ExecutionTrend {
  date: string
  totalExecutions: number
  approved: number
  rejected: number
  deviations: number
  avgRiskScore: number
}

export interface DeviationAnalysisSummary {
  deviationCode: string
  deviationName: string
  severity: Severity
  occurrenceCount: number
  overrideCount: number
  month: string
}

// ============================================================
// CLIENT MANAGEMENT
// ============================================================

export interface Tenant {
  id: string
  tenantCode: string
  tenantName: string
  displayName?: string
  logoUrl?: string
  primaryColor: string
  planType: 'STARTER' | 'PROFESSIONAL' | 'ENTERPRISE'
  maxRules: number
  maxExecutionsPerDay: number
  isActive: boolean
  subscriptionEndDate?: string
  createdAt: string
}

export interface User {
  id: string
  tenantId: string
  email: string
  username: string
  fullName: string
  employeeId?: string
  designation?: string
  department?: string
  mobile?: string
  isActive: boolean
  roles: string[]
  lastLoginAt?: string
  createdAt: string
}

// ============================================================
// MISC
// ============================================================

export interface PagedResponse<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface ApiError {
  status: number
  title: string
  detail?: string
  errors?: Record<string, string[]>
}
