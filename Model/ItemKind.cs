using System.Text.Json.Serialization;

namespace mps.Model
{
    /// <summary>
    /// What a list entry stands for. Serialized as a string ("Product"/"Task"), numbers still parse.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ItemKind
    {
        /// <summary>Something to buy. Purchases are recorded in the item history.</summary>
        Product = 0,

        /// <summary>An errand or reminder ("Tanken", "Apotheke"). Never recorded in the history.</summary>
        Task = 1
    }
}
