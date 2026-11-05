using Azure.Data.Tables;

namespace AlexaSkills.Skills.TimeClock;

/// <summary>
/// Keeps time cards in Azure Table storage, one row per user: PartitionKey is the Alexa user ID
/// and Data holds the time card's JSON.
/// </summary>
public sealed class TableTimeCardStore(TableClient table) : ITimeCardStore
{
    public const string RowKey = "timecard";
    private const string DataColumn = "Data";

    public async Task<TimeCard> LoadAsync(string userId, CancellationToken cancellationToken)
    {
        var row = await table.GetEntityIfExistsAsync<TableEntity>(userId, RowKey, [DataColumn], cancellationToken);
        return TimeCard.FromJson(row.HasValue ? row.Value!.GetString(DataColumn) : null);
    }

    public async Task SaveAsync(string userId, TimeCard timeCard, CancellationToken cancellationToken)
    {
        var row = new TableEntity(userId, RowKey) { [DataColumn] = timeCard.ToJson() };
        await table.UpsertEntityAsync(row, TableUpdateMode.Replace, cancellationToken);
    }
}
