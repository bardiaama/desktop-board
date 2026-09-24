using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Core.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DesktopBoard.App.ViewModels;

public sealed partial class DesktopItemViewModel : ObservableObject
{
    private readonly DesktopDockViewModel _owner;

    public DesktopItemViewModel(DesktopDockViewModel owner, DesktopItem item, WriteableBitmap? icon)
    {
        _owner = owner;
        Item = item;
        Icon = icon;
        LaunchCommand = new RelayCommand(() => _owner.Launch(this));
        ShowInFolderCommand = new RelayCommand(() => _owner.ShowInFolder(this));
    }

    public DesktopItem Item { get; }
    public string Name => Item.Name;
    public WriteableBitmap? Icon { get; }
    public IRelayCommand LaunchCommand { get; }
    public IRelayCommand ShowInFolderCommand { get; }
}

/// <summary>
/// The desktop icons, shown inside the board as a dock. Items come from the Desktop folders
/// and the Recycle Bin; the shell's own icons are hidden while the dock is enabled so the
/// board can cover the whole screen and still give access to everything on the desktop.
/// </summary>
public sealed partial class DesktopDockViewModel : ObservableObject
{
    private const int IconLogicalSize = 48;
    private readonly IDesktopItemsProvider _provider;
    private readonly DispatcherQueue _dispatcher;
    private int _loadVersion;

    public DesktopDockViewModel(IDesktopItemsProvider provider)
    {
        _provider = provider;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _provider.Changed += (_, _) => _dispatcher.TryEnqueue(() => _ = LoadAsync());
    }

    public ObservableCollection<DesktopItemViewModel> Items { get; } = new();

    [ObservableProperty] private bool _isLoading;
    /// <summary>Icon columns; grows with the number of items so the dock never scrolls on a normal desktop.</summary>
    [ObservableProperty] private int _columns = 1;
    /// <summary>Rows that fit the dock's height; the view reports it from its actual size.</summary>
    [ObservableProperty] private int _rowsThatFit = 9;

    partial void OnRowsThatFitChanged(int value) => UpdateColumns();

    public async Task LoadAsync(double rasterizationScale = 1.25)
    {
        var version = ++_loadVersion;
        IsLoading = true;
        try
        {
            var px = (int)Math.Round(IconLogicalSize * Math.Max(1.0, rasterizationScale));
            var items = await _provider.GetItemsAsync(px);
            if (version != _loadVersion) return;

            Items.Clear();
            foreach (var item in items)
                Items.Add(new DesktopItemViewModel(this, item, ToBitmap(item)));
            UpdateColumns();
        }
        catch (Exception ex)
        {
            Logger.Error("Desktop dock load failed", ex);
        }
        finally
        {
            if (version == _loadVersion) IsLoading = false;
        }
    }

    private void UpdateColumns()
    {
        var rows = Math.Max(1, RowsThatFit);
        Columns = Math.Clamp((Items.Count + rows - 1) / rows, 1, 3);
    }

    private static WriteableBitmap? ToBitmap(DesktopItem item)
    {
        if (item.IconBgra is null || item.IconWidth <= 0 || item.IconHeight <= 0) return null;
        var bmp = new WriteableBitmap(item.IconWidth, item.IconHeight);
        using (var s = bmp.PixelBuffer.AsStream()) s.Write(item.IconBgra, 0, item.IconBgra.Length);
        bmp.Invalidate();
        return bmp;
    }

    public void Launch(DesktopItemViewModel vm)
    {
        try { _provider.Launch(vm.Item); }
        catch (Exception ex) { Logger.Error($"Launch failed: {vm.Item.Path}", ex); }
    }

    public void ShowInFolder(DesktopItemViewModel vm)
    {
        try { _provider.ShowInFolder(vm.Item); }
        catch (Exception ex) { Logger.Error($"Show in folder failed: {vm.Item.Path}", ex); }
    }
}
