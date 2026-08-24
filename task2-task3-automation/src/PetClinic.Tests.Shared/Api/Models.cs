namespace PetClinic.Tests.Shared.Api;

public sealed class LoginResponse
{
    public string Token { get; set; } = "";
    public string TokenType { get; set; } = "";
    public UserInfo User { get; set; } = new();
}

public sealed class UserInfo
{
    public string Username { get; set; } = "";
    public string Role { get; set; } = "";
    public bool Enabled { get; set; }
}

public sealed class InvoiceResponse
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal TaxRate { get; set; }
    public decimal DiscountPct { get; set; }
    public List<InvoiceItemResponse> Items { get; set; } = [];
    public List<InvoicePaymentResponse> Payments { get; set; } = [];
    public InvoiceTotals Totals { get; set; } = new();
    public List<string> AllowedTransitions { get; set; } = [];
}

public sealed class InvoiceItemResponse
{
    public int Id { get; set; }
    public string Description { get; set; } = "";
    public string ItemType { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class InvoicePaymentResponse
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "";
}

public sealed class InvoiceTotals
{
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
}

public sealed class InvoiceListResponse
{
    public List<InvoiceSummary> Content { get; set; } = [];
    public int Page { get; set; }
    public int Size { get; set; }
    public int TotalElements { get; set; }
    public int TotalPages { get; set; }
    public bool First { get; set; }
    public bool Last { get; set; }
}

public sealed class InvoiceSummary
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
}

public sealed class OwnerResponse
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string Telephone { get; set; } = "";
    public string Email { get; set; } = "";
}

public sealed class PetResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int TypeId { get; set; }
    public int OwnerId { get; set; }
}
