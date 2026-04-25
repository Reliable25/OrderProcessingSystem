namespace OrderProcessing.Domain.Entities
{
    public class IdempotencyRecord
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string Key { get; private set; } = string.Empty;
        public string RequestHash { get; private set; } = string.Empty;
        public Guid? ResourceId { get; private set; } // e.g., OrderId
        public string? ResponsePayload { get; private set; } // optional serialized response
        public IdempotencyStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
        public string? FailureReason { get; private set; }

        // EF ctor
        private IdempotencyRecord() { }

        public IdempotencyRecord(string key, string requestHash)
        {
            Key = key;
            RequestHash = requestHash;
            Status = IdempotencyStatus.InProgress;
        }

        public void MarkCompleted(Guid resourceId, string responsePayload)
        {
            ResourceId = resourceId;
            ResponsePayload = responsePayload;
            Status = IdempotencyStatus.Completed;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkFailed(string reason)
        {
            Status = IdempotencyStatus.Failed;
            FailureReason = reason;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public enum IdempotencyStatus
    {
        InProgress = 0,
        Completed = 1,
        Failed = 2
    }
}