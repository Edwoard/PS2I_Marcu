using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace Simulator
{
    public enum State {  Stopped, On, Off }
    public enum MixState { Stopped, Clockwise, CouterClockwise }
    public record ProcessState
    {
        public State H1Manual { get; init; }
        public State H2Substrace { get; init; }
        public State H3Catalist { get; init; }
        public State H4InertGaz { get; init; }
        public State H5AutoMode { get; init; }

        public int SupplyRate { get; init; } = 50;
        public int DischargeRate { get; init; } = 50;

        public State CoolantCircut { get; init; }
        public State ProductValve { get; init; }

        public MixState MixState { get; init; }

        public bool IsNewtralised { get; init; }
        public bool LPlus { get; init; }
        public bool LMinus { get; init; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters ={ new JsonStringEnumConverter() }
            });
        }
    }


    class ViewModel : INotifyPropertyChanged
    {
        public bool _isSendingData = true;

        private Comm.Sender _sender = new Comm.Sender("127.0.0.1", 5500);
        List<DelegateCommand> commands = new List<DelegateCommand>();
        public void InvalidateCommands() => this.commands.ForEach(c => c.RaiseCanExecuteChanged());

        public bool IsManualOrAuto => this.ProcessState.H1Manual == State.On || this.ProcessState.H5AutoMode == State.On;
        public ViewModel()
        {
            this.WaterCoolantErrorCommand = new DelegateCommand(
                executeHandler: () => this.WaterCoolantError(),
                canExecuteHandler: () => this.IsManualOrAuto
            );
            this.commands.Add(this.WaterCoolantErrorCommand);

            this.AbortCommand = new DelegateCommand(
                executeHandler: () => this.AbortProcess(),
                canExecuteHandler: () => this.cancelProcess != null
            );
            this.commands.Add(this.AbortCommand);

            this.SetManualModeCommand = new DelegateCommand(
                executeHandler: () =>
                {
                    this.ProcessState = this.ResetProcessState() with
                    { 
                        H1Manual = State.On,
                    };
                },
                canExecuteHandler: () => this.ProcessState.H1Manual != State.On
            );
            this.commands.Add(this.SetManualModeCommand);

            this.SetAutoCommand = new DelegateCommand(
                executeHandler: () => 
                {
                    this.ProcessState = this.ResetProcessState() with 
                    { 
                        H5AutoMode = State.On 
                    };
                },
                canExecuteHandler: () => this.ProcessState.H5AutoMode != State.On
            );
            this.commands.Add(this.SetAutoCommand);

            this.NeutralizeCommand = new DelegateCommand(
                executeHandler: () => this.currentProcessTask = this.TryStartProcess(),
                canExecuteHandler: () => 
                    this.ProcessState.H4InertGaz == State.Stopped
                    && this.IsManualOrAuto
                    && !this.ProcessState.IsNewtralised
            );
            this.commands.Add(this.NeutralizeCommand);

            this.AddCatalistCommand = new DelegateCommand(
                executeHandler: () => this.AddCatalist(),
                canExecuteHandler: () => 
                    this.ProcessState.IsNewtralised
                    && this.ProcessState.CoolantCircut == State.On 
                    && this.ProcessState.H3Catalist == State.Stopped
            );
            this.commands.Add(this.AddCatalistCommand);


            this.AddSubstraceCommand = new DelegateCommand(
                executeHandler: () => this.AddSubstrace(),
                canExecuteHandler: () =>
                    this.ProcessState.IsNewtralised
                    && this.ProcessState.CoolantCircut == State.On 
                    && this.ProcessState.H2Substrace == State.Stopped
            );
            this.commands.Add(this.AddSubstraceCommand);
        }

        ProcessState ResetProcessState()
        {
            return this.ProcessState with
            {
                CoolantCircut = State.Stopped,
                H1Manual = State.Stopped,
                H5AutoMode = State.Stopped,
                MixState = MixState.Stopped,
                H2Substrace = State.Stopped,
                H3Catalist = State.Stopped,
                ProductValve = State.Stopped,
                H4InertGaz = State.Stopped,
                IsNewtralised = false
            };
        }
        void AddCatalist()
        {
            this.ProcessState = this.ProcessState with
            {
                H3Catalist = State.On
            };
        }

        void AddSubstrace()
        {
            this.ProcessState = this.ProcessState with
            {
                H2Substrace = State.On
            };
        }
        async Task AddInertGas(CancellationToken cancellationToken)
        {
            for (int i = 1; i < 10; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.ProcessState = this.ProcessState with { H4InertGaz = State.On };
                await Task.Delay(500);

                cancellationToken.ThrowIfCancellationRequested();
                this.ProcessState = this.ProcessState with { H4InertGaz = State.Off };
                await Task.Delay(500);
            }

            cancellationToken.ThrowIfCancellationRequested();
            this.ProcessState = this.ProcessState with
            {
                H4InertGaz = State.Stopped,
                CoolantCircut = State.On,
                IsNewtralised = true,
            };
        }

        CancellationTokenSource cancelProcess;

        async void WaterCoolantError()
        {
            this.AbortProcess();
            await this.currentProcessTask;
            this.ProcessState = this.ProcessState with
            {
                ProductValve = State.On,
                CoolantCircut = State.Stopped
            };
        }
        void AbortProcess()
        {
            cancelProcess?.Cancel();
        }
        Task currentProcessTask;
        async Task TryStartProcess()
        {
            try
            {
                cancelProcess = new CancellationTokenSource();
                await this.AddInertGas(cancelProcess.Token);
                if(this.ProcessState.H5AutoMode == State.On)
                {
                    this.AddCatalist();
                    this.AddSubstrace();
                }
                await this.WaitForFillAndMixProduct(cancelProcess.Token);
            }
            catch (OperationCanceledException)
            {
                this.ProcessState = this.ResetProcessState() with
                {
                    CoolantCircut = State.On,
                };
            }
            cancelProcess = null;
        }
        async Task WaitForFillAndMixProduct(CancellationToken cancellationToken)
        {
            while (!this.ProcessState.LPlus)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(1);
            }
            for (int i = 0; i < 2; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.ProcessState = this.ProcessState with
                {
                    MixState = MixState.Clockwise
                };
                await Task.Delay(2000);
                cancellationToken.ThrowIfCancellationRequested();
                this.ProcessState = this.ProcessState with
                {
                    MixState = MixState.CouterClockwise
                };
                await Task.Delay(2000);
                cancellationToken.ThrowIfCancellationRequested();
            }
            this.ProcessState = this.ProcessState with
            {
                MixState = MixState.Clockwise
            };
            await Task.Delay(2000);
            cancellationToken.ThrowIfCancellationRequested();

            this.ProcessState = this.ProcessState with
            {
                MixState = MixState.Stopped,
                ProductValve = State.On
            };
            while (!this.ProcessState.LMinus)
            {
                await Task.Delay(1);
                cancellationToken.ThrowIfCancellationRequested();
            }
            this.ProcessState = this.ResetProcessState();
        }

        //  de aici in jos este implementata partea de simulare a procesului
        #region Simulator

        private ProcessState _currentStateOfTheProcess = new ProcessState();
        public ProcessState ProcessState
        {
            get => _currentStateOfTheProcess;
            set
            {
                this.SetProperty(ref _currentStateOfTheProcess, value);
                this.InvalidateCommands();
                this._sender.Send(_currentStateOfTheProcess.ToString());
            }
        }

        public DelegateCommand AbortCommand { get; }
        public DelegateCommand SetManualModeCommand { get; }
        public DelegateCommand SetAutoCommand { get; }
        public DelegateCommand NeutralizeCommand { get; }
        public DelegateCommand AddCatalistCommand { get; }
        public DelegateCommand AddSubstraceCommand { get; }

        public bool LPlus
        {
            get => this.ProcessState.LPlus;
            set => this.ProcessState = this.ProcessState with { LPlus = value };
        }

        public bool LMinus
        {
            get => this.ProcessState.LMinus;
            set => this.ProcessState = this.ProcessState with { LMinus = value };
        }

        public int DischargeRate
        {
            get => this.ProcessState.DischargeRate;
            set => this.ProcessState = this.ProcessState with { DischargeRate = value };
        }


        public int SupplyRate
        {
            get => this.ProcessState.SupplyRate;
            set => this.ProcessState = this.ProcessState with { SupplyRate = value };
        }
        public DelegateCommand WaterCoolantErrorCommand { get; }
        #endregion


        #region VM
        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string prop)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }

        void SetProperty<T>(ref T field, T value, [CallerMemberName] string prop = null)
        {
            if(!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                OnPropertyChanged(prop);
            }
        }
        #endregion
    }

    public class DelegateCommand : ICommand
    {
        private readonly Func<bool> canExecuteHandler;
        private readonly Action executeHandler;

        public DelegateCommand(Action executeHandler, Func<bool> canExecuteHandler = null)
        {
            this.canExecuteHandler = canExecuteHandler;
            this.executeHandler = executeHandler;
        }
        public event EventHandler CanExecuteChanged;

        public void RaiseCanExecuteChanged()
        {
            this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool CanExecute(object parameter)
        {
            return this.canExecuteHandler?.Invoke() ?? true;
        }

        public void Execute(object parameter)
        {
            this.executeHandler();
        }
    }
}
