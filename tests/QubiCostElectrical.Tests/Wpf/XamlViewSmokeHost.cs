using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Hilo STA compartido que hospeda la única <c>Application</c> con <c>Theme.xaml</c> fusionado
/// para los smoke tests que cargan XAML real (InitializeComponent).
///
/// Dos razones para centralizarlo aquí:
/// 1. WPF sólo admite UNA <c>Application</c> por AppDomain. Si dos clases de test crean la suya,
///    la segunda revienta con "No se puede crear más de una instancia System.Windows.Application
///    en el mismo AppDomain" y el host de pruebas se anula (hallado el 11-10: el smoke de Costo
///    abortó el de Plantillas).
/// 2. Los recursos estáticos del XAML sólo resuelven si existe una Application con el tema
///    fusionado, y la Application y la vista deben vivir en el mismo hilo STA (afinidad de
///    Dispatcher).
///
/// <c>Application.Current</c> es global: dejarlo no-nulo contamina al resto de la suite (refrescos
/// que se encolan en un dispatcher sin pump y tests que cuelgan). Por eso cada acción limpia el
/// campo interno al terminar y lo restaura al empezar.
/// </summary>
public static class XamlViewSmokeHost
{
    private static readonly BlockingCollection<Action> uiQueue = new();
    private static readonly ManualResetEventSlim uiReady = new(false);
    private static readonly object startLock = new();
    private static bool started;
    private static System.Windows.Application? app;

    /// <summary>Ejecuta la acción en el hilo STA compartido y repropaga cualquier excepción.</summary>
    public static void Run(Action action)
    {
        Start();
        ExceptionDispatchInfo? captured = null;
        using var done = new ManualResetEventSlim(false);
        uiQueue.Add(() =>
        {
            try
            {
                RestoreApplicationCurrent();
                action();
            }
            catch (Exception ex) { captured = ExceptionDispatchInfo.Capture(ex); }
            finally { done.Set(); }
        });
        done.Wait();
        captured?.Throw();
    }

    /// <summary>
    /// La suite no corre Dispatcher.Run, así que las transferencias de binding diferidas
    /// quedarían pendientes para siempre. Este frame procesa lo encolado hasta ApplicationIdle.
    /// </summary>
    public static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    /// <summary>Anula Application.Current para no contaminar al resto de la suite.</summary>
    public static void ClearApplicationCurrent()
    {
        foreach (var type in typeof(System.Windows.Application).Assembly.GetTypes())
        {
            FieldInfo[] fields;
            try { fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static); }
            catch { continue; }

            foreach (var field in fields)
                if (field.FieldType == typeof(System.Windows.Application))
                    try { field.SetValue(null, null); } catch { }
        }
    }

    private static void RestoreApplicationCurrent()
    {
        if (app == null) return;
        foreach (var type in typeof(System.Windows.Application).Assembly.GetTypes())
        {
            FieldInfo[] fields;
            try { fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static); }
            catch { continue; }

            foreach (var field in fields)
                if (field.FieldType == typeof(System.Windows.Application) && field.GetValue(null) == null)
                    try { field.SetValue(null, app); } catch { }
        }
    }

    private static void Start()
    {
        if (Volatile.Read(ref started)) return;
        lock (startLock)
        {
            if (started) return;
            var thread = new Thread(() =>
            {
                var instance = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                instance.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/QubiCostElectrical;component/DesignSystem/Theme.xaml")
                });
                app = instance;
                Volatile.Write(ref started, true);
                uiReady.Set();
                foreach (var work in uiQueue.GetConsumingEnumerable())
                    work();
            })
            {
                IsBackground = true,
                Name = "XamlViewSmoke_STA"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!uiReady.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("El hilo STA con la Application no arrancó en 30 s.");
        }
    }
}
