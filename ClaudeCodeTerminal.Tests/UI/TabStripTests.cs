using System.Drawing;
using System.Runtime.InteropServices;
using ClaudeCodeTerminal.App.UI;

namespace ClaudeCodeTerminal.Tests.UI;

public class TabStripTests
{
    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;

    private static void RunOnSta(Action action)
    {
        var thread = new Thread(() => action());
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void AddTab_AppearsInItemsWithGivenName()
    {
        TabStripItem? item = null;

        RunOnSta(() =>
        {
            using var strip = new TabStrip();
            item = strip.AddTab("Session 1");

            Assert.Single(strip.Items);
            Assert.Same(item, strip.Items[0]);
        });

        Assert.Equal("Session 1", item!.SessionName);
    }

    [Fact]
    public void Activate_SetsIsActiveAndRaisesTabActivated()
    {
        var raisedCount = 0;
        TabStripItem? raisedWith = null;

        RunOnSta(() =>
        {
            using var strip = new TabStrip();
            var item1 = strip.AddTab("Session 1");
            var item2 = strip.AddTab("Session 2");
            strip.TabActivated += (_, item) =>
            {
                raisedCount++;
                raisedWith = item;
            };

            strip.Activate(item2);

            Assert.False(item1.IsActive);
            Assert.True(item2.IsActive);
        });

        Assert.Equal(1, raisedCount);
        Assert.Equal("Session 2", raisedWith!.SessionName);
    }

    [Fact]
    public void Activate_SameItemTwiceDoesNotRaiseTabActivatedAgain()
    {
        var raisedCount = 0;

        RunOnSta(() =>
        {
            using var strip = new TabStrip();
            var item = strip.AddTab("Session 1");
            strip.TabActivated += (_, _) => raisedCount++;

            strip.Activate(item);
            strip.Activate(item);
        });

        Assert.Equal(1, raisedCount);
    }

    [Fact]
    public void RemoveTab_OfNonActiveTabDoesNotChangeActiveTabOrRaiseTabActivated()
    {
        var raisedCount = 0;

        RunOnSta(() =>
        {
            using var strip = new TabStrip();
            var item1 = strip.AddTab("Session 1");
            var item2 = strip.AddTab("Session 2");
            strip.Activate(item2);
            strip.TabActivated += (_, _) => raisedCount++;

            strip.RemoveTab(item1);

            Assert.Single(strip.Items);
            Assert.True(item2.IsActive);
        });

        Assert.Equal(0, raisedCount);
    }

    [Fact]
    public void RemoveTab_OfActiveTabActivatesANeighbor()
    {
        TabStripItem? activatedAfterRemoval = null;

        RunOnSta(() =>
        {
            using var strip = new TabStrip();
            var item1 = strip.AddTab("Session 1");
            var item2 = strip.AddTab("Session 2");
            var item3 = strip.AddTab("Session 3");
            strip.Activate(item2);

            strip.TabActivated += (_, item) => activatedAfterRemoval = item;
            strip.RemoveTab(item2);

            Assert.Equal(2, strip.Items.Count);
            Assert.DoesNotContain(item2, strip.Items);
        });

        Assert.NotNull(activatedAfterRemoval);
        Assert.True(activatedAfterRemoval!.IsActive);
    }

    [Fact]
    public void RemoveTab_LastRemainingTabLeavesNoneActiveWithoutThrowing()
    {
        var raisedCount = 0;

        RunOnSta(() =>
        {
            using var strip = new TabStrip();
            var item = strip.AddTab("Session 1");
            strip.Activate(item);
            strip.TabActivated += (_, _) => raisedCount++;

            strip.RemoveTab(item);

            Assert.Empty(strip.Items);
        });

        Assert.Equal(0, raisedCount);
    }

    [Fact]
    public void ClickingCloseButtonOnItemBubblesUpAsTabCloseRequested()
    {
        TabStripItem? raisedWith = null;

        RunOnSta(() =>
        {
            using var form = new Form { Width = 400, Height = 200, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) };
            using var strip = new TabStrip();
            form.Controls.Add(strip);
            form.Show();

            var item = strip.AddTab("Session 1");
            strip.TabCloseRequested += (_, closedItem) => raisedWith = closedItem;

            // The close "x" sits near the right edge of the item, well clear of the rename-trigger
            // label area on the left.
            var lParam = (nint)(((item.Height / 2) << 16) | (item.Width - 10));
            SendMessage(item.Handle, WM_LBUTTONDOWN, (nint)1, lParam);
            SendMessage(item.Handle, WM_LBUTTONUP, (nint)0, lParam);
        });

        Assert.NotNull(raisedWith);
        Assert.Equal("Session 1", raisedWith!.SessionName);
    }
}
