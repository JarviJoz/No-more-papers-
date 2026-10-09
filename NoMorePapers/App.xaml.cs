using System;
using System.Windows;

namespace NoMorePapers
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; set; } = null!;
    }
}