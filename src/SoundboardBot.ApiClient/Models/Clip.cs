using Postgrest.Attributes;
using Postgrest.Models;
namespace SoundboardBot.ApiClient.Models; 

[Table("Clips")]
public class Clip : BaseModel {
    [PrimaryKey(ColumnName = "id")]
    public Guid Id { get; set; }
    
    [Column(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; }

    [Column(ColumnName = "key")]
    public string Key { get; set; } = null!;

    [Column(ColumnName = "url")]
    public string Url { get; set; } = null!;

    [Column(ColumnName = "description")]
    public string Description { get; set; } = null!;
}
