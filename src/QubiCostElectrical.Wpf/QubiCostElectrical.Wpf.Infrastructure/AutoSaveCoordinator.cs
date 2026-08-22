#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure.Diagnostics;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed class AutoSaveCoordinator : INotifyPropertyChanged, IDisposable
{
	private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30L);

	private readonly BudgetProjectSession session;

	private readonly IAutoSaveWorkflow workflow;

	private readonly IAppLogger? logger;

	private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

	private CancellationTokenSource cancellation = new CancellationTokenSource();

	private Timer? timer;

	private bool pending;

	private bool disposed;

	private string statusText = "";

	private DateTime? lastAutoSaveAt;

	public string StatusText
	{
		get
		{
			return statusText;
		}
		private set
		{
			if (!(statusText == value))
			{
				statusText = value;
				OnPropertyChanged("StatusText");
			}
		}
	}

	public DateTime? LastAutoSaveAt
	{
		get
		{
			return lastAutoSaveAt;
		}
		private set
		{
			if (!(lastAutoSaveAt == value))
			{
				lastAutoSaveAt = value;
				OnPropertyChanged("LastAutoSaveAt");
				OnPropertyChanged("LastAutoSaveAgo");
			}
		}
	}

	public string LastAutoSaveAgo
	{
		get
		{
			if (lastAutoSaveAt.HasValue)
			{
				return FormatAgo(DateTime.UtcNow - lastAutoSaveAt.Value);
			}
			return "";
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public AutoSaveCoordinator(BudgetProjectSession session, IAutoSaveWorkflow workflow, IAppLogger? logger)
	{
		this.session = session ?? throw new ArgumentNullException("session");
		this.workflow = workflow ?? throw new ArgumentNullException("workflow");
		this.logger = logger;
	}

	public void Start()
	{
		if (!disposed)
		{
			Stop();
			cancellation = new CancellationTokenSource();
			StatusText = "Auto-guardado activo";
			timer = new Timer(delegate(object? _)
			{
				_ = TickAsync();
			}, null, Interval, Interval);
		}
	}

	public void Stop()
	{
		timer?.Dispose();
		timer = null;
		cancellation.Cancel();
		StatusText = "";
		LastAutoSaveAt = null;
	}

	public void MarkSaved()
	{
		LastAutoSaveAt = DateTime.UtcNow;
	}

	public void Dispose()
	{
		if (!disposed)
		{
			disposed = true;
			Stop();
			cancellation.Dispose();
			gate.Dispose();
		}
	}

	private void OnPropertyChanged([CallerMemberName] string? name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	private async Task TickAsync()
	{
		if (disposed || !session.IsDirty)
		{
			return;
		}
		if (!(await gate.WaitAsync(0, cancellation.Token).ConfigureAwait(continueOnCapturedContext: false)))
		{
			pending = true;
			return;
		}
		try
		{
			AutoSaveResult autoSaveResult = await workflow.SaveSnapshotAsync(session, cancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
			if (autoSaveResult.Success)
			{
				LastAutoSaveAt = autoSaveResult.SavedAtUtc;
				StatusText = "Auto-guardado completado";
			}
			else
			{
				StatusText = "Error al auto-guardar";
				logger?.Warning("AutoSave", autoSaveResult.ErrorMessage ?? "No se pudo autoguardar.");
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex2)
		{
			StatusText = "Error al auto-guardar";
			logger?.Error("AutoSave", "Error en autoguardado", ex2);
		}
		finally
		{
			gate.Release();
			if (pending)
			{
				pending = false;
				_ = TickAsync();
			}
		}
	}

	private static string FormatAgo(TimeSpan ago)
	{
		if (!(ago.TotalSeconds < 5.0))
		{
			if (!(ago.TotalSeconds < 60.0))
			{
				if (ago.TotalMinutes < 60.0)
				{
					return $"hace {(int)ago.TotalMinutes}min";
				}
				return $"hace {(int)ago.TotalHours}h";
			}
			return $"hace {(int)ago.TotalSeconds}s";
		}
		return "ahora";
	}
}
