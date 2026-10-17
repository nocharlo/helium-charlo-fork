using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace HeliumHybrid
{
    public sealed class ActionCommand : ICommand
    {
        private readonly Action action;
        public ActionCommand(Action action) { this.action = action; }
        public event EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter) { action(); }
    }

    // Bindings in Library.xaml and LibraryGridView.xaml use these runtime properties.
    public sealed class AdaptiveState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private bool compact, detailPage, inGrid = true, navigatorVisible = true;
        public bool IsCompact { get { return compact; } set { if (compact != value) { compact = value; Notify(); Notify(nameof(SuppressDetails)); Notify(nameof(IsDetailPage)); } } }
        public bool IsDetailPage { get { return detailPage; } set { if (detailPage != value) { detailPage = value; Notify(); Notify(nameof(SuppressDetails)); Notify(nameof(BackVisibility)); } } }
        public bool SuppressDetails => compact && !detailPage;
        public bool InGrid { get { return inGrid; } set { if (inGrid != value) { inGrid = value; Notify(); } } }
        public bool NavigatorVisible { get { return navigatorVisible; } set { if (navigatorVisible != value) { navigatorVisible = value; Notify(); Notify(nameof(RevealVisible)); } } }
        public bool RevealVisible => !navigatorVisible;
        public Visibility BackVisibility => detailPage ? Visibility.Visible : Visibility.Collapsed;
        public ICommand BackCommand { get; set; }
        private void Notify([System.Runtime.CompilerServices.CallerMemberName] string name = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
    }
}
