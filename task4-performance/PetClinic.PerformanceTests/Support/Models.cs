using System.Text.Json.Serialization;

namespace PetClinic.PerformanceTests.Support;

public sealed class LoginResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = "";
}

public sealed class OwnerResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

public sealed class UserResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";
}

public sealed class InvoiceResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("invoiceNo")]
    public string InvoiceNo { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("payments")]
    public List<PaymentResponse> Payments { get; set; } = [];

    [JsonPropertyName("totals")]
    public InvoiceTotals Totals { get; set; } = new();
}

public sealed class PaymentResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}

public sealed class InvoiceTotals
{
    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("amountPaid")]
    public decimal AmountPaid { get; set; }

    [JsonPropertyName("balance")]
    public decimal Balance { get; set; }
}

public sealed class InvoiceListResponse
{
    [JsonPropertyName("content")]
    public List<object> Content { get; set; } = [];

    [JsonPropertyName("totalElements")]
    public int TotalElements { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }
}
