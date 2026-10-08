namespace ClaudeCodeTerminal.App.UI;

public sealed class TabStrip : Panel
{
    private readonly FlowLayoutPanel _itemsPanel;
    private readonly TabStripAddButton _addButton;
    private readonly List<TabStripItem> _items = [];
    private TabStripItem? _activeItem;

    public event EventHandler? AddTabRequested;
    public event EventHandler<TabStripItem>? TabActivated;
    public event EventHandler<TabStripItem>? TabCloseRequested;
    public event EventHandler<TabStripItem>? TabRenamed;

    public IReadOnlyList<TabStripItem> Items => _items;

    public TabStrip()
    {
        Dock = DockStyle.Top;
        Height = 42;
        BackColor = Theme.PanelBackground;

        _itemsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.PanelBackground,
        };

        _addButton = new TabStripAddButton { Margin = new Padding(2, 2, 0, 0) };
        _addButton.Activated += (_, _) => AddTabRequested?.Invoke(this, EventArgs.Empty);

        _itemsPanel.Controls.Add(_addButton);

        Controls.Add(_itemsPanel);
    }

    public TabStripItem AddTab(string name)
    {
        var item = new TabStripItem(name) { Margin = new Padding(0, 2, 2, 0) };
        item.Activated += (_, _) => Activate(item);
        item.CloseRequested += (_, _) => TabCloseRequested?.Invoke(this, item);
        item.Renamed += (_, _) => TabRenamed?.Invoke(this, item);

        _items.Add(item);
        _itemsPanel.Controls.Add(item);
        _itemsPanel.Controls.SetChildIndex(_addButton, _itemsPanel.Controls.Count - 1);

        return item;
    }

    public void RemoveTab(TabStripItem item)
    {
        var wasActive = _activeItem == item;
        var index = _items.IndexOf(item);
        if (index < 0)
            return;

        _items.RemoveAt(index);
        _itemsPanel.Controls.Remove(item);
        if (_activeItem == item)
            _activeItem = null;
        item.Dispose();

        if (wasActive && _items.Count > 0)
            Activate(_items[Math.Min(index, _items.Count - 1)]);
    }

    public void Activate(TabStripItem item)
    {
        if (_activeItem == item)
            return;

        if (_activeItem is not null)
            _activeItem.IsActive = false;

        _activeItem = item;
        item.IsActive = true;
        TabActivated?.Invoke(this, item);
    }
}
