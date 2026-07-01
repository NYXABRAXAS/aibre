using System;
using System.Collections.Generic;

namespace OptimAI.BRE.Shared.Domain;

// ============================================================
// BASE ENTITIES
// ============================================================

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public abstract class TenantEntity : BaseEntity
{
    public Guid TenantId { get; set; }
}

// ============================================================
// TENANT
// ============================================================

public class Tenant : BaseEntity
{
    public string TenantCode { get; set; } = default!;
    public string TenantName { get; set; } = default!;
    public string? DisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#1E40AF";
    public string SecondaryColor { get; set; } = "#3B82F6";
    public PlanType PlanType { get; set; } = PlanType.Enterprise;
    public int MaxRules { get; set; } = 1000;
    public long MaxExecutionsPerDay { get; set; } = 1_000_000;
    public bool IsActive { get; set; } = true;
    public DateTime? TrialEndDate { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }
    public Dictionary<string, object> Settings { get; set; } = new();

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Rule> Rules { get; set; } = new List<Rule>();
}

public enum PlanType { Starter, Professional, Enterprise }

// ============================================================
// IDENTITY
// ============================================================

public class User : TenantEntity
{
    public string Email { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? EmployeeId { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? Mobile { get; set; }
    public string? ProfileImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; }
    public bool IsMfaEnabled { get; set; }
    public string? MfaSecret { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? PasswordChangedAt { get; set; }

    public Tenant Tenant { get; set; } = default!;
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class Role : TenantEntity
{
    public string RoleCode { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class Permission : BaseEntity
{
    public string PermissionCode { get; set; } = default!;
    public string PermissionName { get; set; } = default!;
    public string Module { get; set; } = default!;
    public string? Description { get; set; }
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public Guid? GrantedBy { get; set; }
    public Role Role { get; set; } = default!;
    public Permission Permission { get; set; } = default!;
}

public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public Guid? AssignedBy { get; set; }
    public User User { get; set; } = default!;
    public Role Role { get; set; } = default!;
}

public class ApiKey : TenantEntity
{
    public string KeyName { get; set; } = default!;
    public string ApiKeyHash { get; set; } = default!;
    public string ApiKeyPrefix { get; set; } = default!;
    public List<string> Scopes { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int RateLimitPerMinute { get; set; } = 1000;
    public Guid CreatedBy { get; set; }
}

// ============================================================
// RULE ENGINE CORE
// ============================================================

public class Rule : TenantEntity
{
    public string RuleCode { get; set; } = default!;
    public string RuleName { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public RuleType RuleType { get; set; }
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
    public bool IsPublished { get; set; }
    public bool IsDraft { get; set; } = true;
    public RuleStatus Status { get; set; } = RuleStatus.Draft;
    public Guid? CurrentVersionId { get; set; }
    public List<string> Tags { get; set; } = new();
    public Guid CreatedBy { get; set; }

    public RuleCategory? Category { get; set; }
    public RuleVersion? CurrentVersion { get; set; }
    public ICollection<RuleVersion> Versions { get; set; } = new List<RuleVersion>();
    public ICollection<RuleScope> Scopes { get; set; } = new List<RuleScope>();
}

public enum RuleType
{
    Eligibility, Credit, Bureau, FI, Valuation,
    Fraud, Compliance, Deviation, Income, Kyc,
    Vehicle, Guarantor, CollateralCheck
}

public enum RuleStatus { Draft, PendingApproval, Approved, Published, Archived }

public class RuleVersion : TenantEntity
{
    public Guid RuleId { get; set; }
    public int VersionNumber { get; set; }
    public string? VersionLabel { get; set; }
    public RuleDefinition RuleDefinition { get; set; } = default!;
    public string? ChangeSummary { get; set; }
    public bool IsCurrent { get; set; }
    public RuleStatus Status { get; set; } = RuleStatus.Draft;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? PublishedBy { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid CreatedBy { get; set; }

    public Rule Rule { get; set; } = default!;
}

public class RuleDefinition
{
    public ConditionGroup Conditions { get; set; } = default!;
    public List<RuleAction> Actions { get; set; } = new();
    public RuleMetadata Metadata { get; set; } = new();
}

public class ConditionGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public LogicalOperator Operator { get; set; } = LogicalOperator.And;
    public List<ConditionNode> Rules { get; set; } = new();
}

public class ConditionNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public bool IsGroup { get; set; }
    // When IsGroup = false (leaf condition)
    public string? Field { get; set; }
    public ComparisonOperator? Operator { get; set; }
    public object? Value { get; set; }
    public string? Value2 { get; set; }  // for BETWEEN
    public ValueType ValueType { get; set; } = ValueType.Literal;
    public string? ReferenceField { get; set; }  // for FIELD_COMPARE
    public string? CustomFunction { get; set; }
    // When IsGroup = true
    public ConditionGroup? Group { get; set; }
}

public enum LogicalOperator { And, Or, Not }

public enum ComparisonOperator
{
    Equals, NotEquals,
    GreaterThan, GreaterThanOrEqual,
    LessThan, LessThanOrEqual,
    Between, NotBetween,
    In, NotIn,
    Contains, NotContains,
    StartsWith, EndsWith,
    IsNull, IsNotNull,
    IsTrue, IsFalse,
    Regex, FieldCompare
}

public enum ValueType { Literal, Field, Function, Expression }

public class RuleAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ActionType Type { get; set; }
    public string? Value { get; set; }
    public string? Field { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}

public enum ActionType
{
    SetDecision,
    SetRisk,
    SetTrafficLight,
    AddDeviation,
    SetField,
    SendNotification,
    TriggerWorkflow,
    AddTag,
    SetScore
}

public class RuleMetadata
{
    public int ExecutionOrder { get; set; } = 0;
    public bool StopOnMatch { get; set; } = false;
    public ErrorHandling ErrorHandling { get; set; } = ErrorHandling.Skip;
    public string? Notes { get; set; }
}

public enum ErrorHandling { Skip, Fail, UseDefault }

public class RuleScope : TenantEntity
{
    public Guid RuleId { get; set; }
    public ScopeType ScopeType { get; set; }
    public string ScopeValue { get; set; } = default!;
    public bool IsExcluded { get; set; }
    public Rule Rule { get; set; } = default!;
}

public enum ScopeType { Product, Branch, Stage, UserRole, Global }

public class RuleCategory : TenantEntity
{
    public string CategoryCode { get; set; } = default!;
    public string CategoryName { get; set; } = default!;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class RuleSet : TenantEntity
{
    public string SetCode { get; set; } = default!;
    public string SetName { get; set; } = default!;
    public string? Description { get; set; }
    public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.All;
    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }

    public ICollection<RuleSetMember> Members { get; set; } = new List<RuleSetMember>();
}

public enum ExecutionMode { All, FirstMatch, Scored }

public class RuleSetMember
{
    public Guid SetId { get; set; }
    public Guid RuleId { get; set; }
    public int SortOrder { get; set; }
    public decimal Weight { get; set; } = 1.0m;
    public RuleSet RuleSet { get; set; } = default!;
    public Rule Rule { get; set; } = default!;
}

// ============================================================
// EXECUTION
// ============================================================

public class ExecutionRequest : TenantEntity
{
    public string? CorrelationId { get; set; }
    public string? ApplicationId { get; set; }
    public string? ProductCode { get; set; }
    public string? BranchCode { get; set; }
    public string? StageCode { get; set; }
    public Guid? RuleSetId { get; set; }
    public Dictionary<string, object> InputPayload { get; set; } = new();
    public string? InputHash { get; set; }
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;
    public int Priority { get; set; } = 5;
    public string? SourceSystem { get; set; }
    public Guid? ApiKeyId { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? ProcessingMs { get; set; }
}

public enum ExecutionStatus { Pending, Processing, Completed, Failed }

public class ExecutionResult : TenantEntity
{
    public Guid RequestId { get; set; }
    public Decision FinalDecision { get; set; }
    public decimal? RiskScore { get; set; }
    public RiskCategory? RiskCategory { get; set; }
    public TrafficLight? TrafficLight { get; set; }
    public int TotalRulesEvaluated { get; set; }
    public int RulesPassed { get; set; }
    public int RulesFailed { get; set; }
    public int RulesSkipped { get; set; }
    public int DeviationsCount { get; set; }
    public int? ExecutionMs { get; set; }
    public List<RuleExecutionSummary> RuleResults { get; set; } = new();
    public Dictionary<string, object> FieldValues { get; set; } = new();
    public string? AiSummary { get; set; }
    public AiAnalysis? AiAnalysis { get; set; }
}

public enum Decision { Approve, Reject, Deviation, Refer, Pending }
public enum RiskCategory { Low, Medium, High, Critical }
public enum TrafficLight { Green, Amber, Red }

public class RuleExecutionSummary
{
    public Guid RuleId { get; set; }
    public string RuleCode { get; set; } = default!;
    public string RuleName { get; set; } = default!;
    public int VersionNumber { get; set; }
    public bool IsMatched { get; set; }
    public List<ConditionEvaluationResult> ConditionsEvaluated { get; set; } = new();
    public List<string> ActionsExecuted { get; set; } = new();
    public int? ExecutionMs { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ConditionEvaluationResult
{
    public string ConditionId { get; set; } = default!;
    public string Field { get; set; } = default!;
    public string Operator { get; set; } = default!;
    public object? ExpectedValue { get; set; }
    public object? ActualValue { get; set; }
    public bool Result { get; set; }
}

public class AiAnalysis
{
    public string RiskSummary { get; set; } = default!;
    public string CreditSummary { get; set; } = default!;
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
    public string DeviationsSummary { get; set; } = default!;
    public string ApprovalRecommendation { get; set; } = default!;
    public List<string> RejectionReasons { get; set; } = new();
    public List<string> AdditionalDocuments { get; set; } = new();
    public string UnderwritingNotes { get; set; } = default!;
    public double ConfidenceScore { get; set; }
}

// ============================================================
// DEVIATIONS
// ============================================================

public class DeviationType : TenantEntity
{
    public string DeviationCode { get; set; } = default!;
    public string DeviationName { get; set; } = default!;
    public string? Category { get; set; }
    public Severity DefaultSeverity { get; set; } = Severity.Medium;
    public string? Description { get; set; }
    public string? RecommendedAction { get; set; }
    public bool RequiresApproval { get; set; }
    public string? ApproverRole { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum Severity { Low, Medium, High, Critical }

public class ExecutionDeviation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResultId { get; set; }
    public Guid? RuleId { get; set; }
    public Guid? DeviationTypeId { get; set; }
    public string DeviationCode { get; set; } = default!;
    public string DeviationName { get; set; } = default!;
    public Severity Severity { get; set; }
    public string Reason { get; set; } = default!;
    public string? FieldPath { get; set; }
    public string? ActualValue { get; set; }
    public string? ExpectedValue { get; set; }
    public string? RecommendedAction { get; set; }
    public bool IsOverridden { get; set; }
    public Guid? OverriddenBy { get; set; }
    public string? OverrideReason { get; set; }
    public DateTime? OverrideAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid TenantId { get; set; }
}

// ============================================================
// AUDIT
// ============================================================

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string? EntityId { get; set; }
    public Dictionary<string, object>? OldValues { get; set; }
    public Dictionary<string, object>? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestId { get; set; }
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
