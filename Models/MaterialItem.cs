namespace BueroCockpit.Models;

public sealed class MaterialItem : ObservableObject
{
    private decimal _quantity = 1;
    private string _unit = "Stk.";
    private string _name = string.Empty;
    private string _status = "Offen";
    private string _supplier = string.Empty;
    private DateTime? _orderedAt;
    private string _note = string.Empty;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = string.Empty;
    public decimal Quantity { get => _quantity; set => SetProperty(ref _quantity, value); }
    public string Unit { get => _unit; set => SetProperty(ref _unit, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public string Supplier { get => _supplier; set => SetProperty(ref _supplier, value); }
    public DateTime? OrderedAt { get => _orderedAt; set => SetProperty(ref _orderedAt, value); }
    public string Note { get => _note; set => SetProperty(ref _note, value); }
}
