namespace NightMarket.WebApi.Domain.Common.Contracts;

/// <summary>
/// Marker interface cho entities hỗ trợ soft delete.
/// Entities implement interface này sẽ:
/// - Được đánh dấu DeletedOn/DeletedBy thay vì xóa vật lý
/// - Tự động bị exclude khỏi queries (via global query filter)
/// - Có thể restore bằng cách set DeletedOn = null
/// </summary>
public interface ISoftDelete
{
    DateTime? DeletedOn { get; set; }
    Guid? DeletedBy { get; set; }
}
