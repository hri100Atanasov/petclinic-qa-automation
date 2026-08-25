using System.Globalization;
using Microsoft.Playwright;

namespace PetClinic.Tests.Ui.Pages;

public class InvoiceDetailPage(IPage page)
{
    public ILocator Status => page.GetByTestId("invoice-status");

    // No data-testid on this one -- the detail page only tags it with a "Due"
    // label div immediately followed by a plain, untagged value div.
    // Adjacent-sibling CSS locator instead.
    public ILocator DueDate => page.Locator("div.muted:text-is('Due') + div");

    public ILocator Subtotal => page.GetByTestId("totals-subtotal");
    public ILocator TaxableAmount => page.GetByTestId("totals-taxable");
    public ILocator TaxAmount => page.GetByTestId("totals-tax");
    public ILocator Total => page.GetByTestId("totals-total");
    public ILocator Paid => page.GetByTestId("totals-paid");
    public ILocator Balance => page.GetByTestId("totals-balance");

    public ILocator AddLineItemButton => page.GetByTestId("item-add-button");
    public ILocator IssueButton => page.GetByTestId("invoice-issue-button");
    public ILocator PayButton => page.GetByTestId("invoice-pay-button");
    public ILocator VoidButton => page.GetByTestId("invoice-void-button");

    // Line-item modal
    private ILocator ItemModal => page.GetByTestId("item-modal");
    private ILocator ItemDescriptionInput => page.GetByTestId("item-description-input");
    private ILocator ItemTypeSelect => page.GetByTestId("item-type-select");
    private ILocator ItemQuantityInput => page.GetByTestId("item-quantity-input");
    private ILocator ItemPriceInput => page.GetByTestId("item-price-input");
    private ILocator ItemSaveButton => page.GetByTestId("item-save");

    // Payment modal
    private ILocator PaymentModal => page.GetByTestId("payment-modal");
    private ILocator PaymentAmountInput => page.GetByTestId("payment-amount-input");
    private ILocator PaymentMethodSelect => page.GetByTestId("payment-method-select");
    private ILocator PaymentSaveButton => page.GetByTestId("payment-save");

    public Task NavigateAsync(string baseUrl, int invoiceId) => page.GotoAsync($"{baseUrl}/invoices/{invoiceId}");

    public async Task AddLineItemAsync(string description, string itemType, int quantity, decimal unitPrice)
    {
        await AddLineItemButton.ClickAsync();
        await ItemDescriptionInput.FillAsync(description);
        await ItemTypeSelect.SelectOptionAsync(itemType);
        await ItemQuantityInput.FillAsync(quantity.ToString(CultureInfo.InvariantCulture));
        await ItemPriceInput.FillAsync(unitPrice.ToString(CultureInfo.InvariantCulture));
        await ItemSaveButton.ClickAsync();
        await ItemModal.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
    }

    public Task IssueInvoiceAsync() => IssueButton.ClickAsync();

    public async Task RecordPaymentAsync(decimal amount, string method)
    {
        await PayButton.ClickAsync();
        await PaymentAmountInput.FillAsync(amount.ToString(CultureInfo.InvariantCulture));
        await PaymentMethodSelect.SelectOptionAsync(method);
        await PaymentSaveButton.ClickAsync();
        await PaymentModal.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
    }
}
