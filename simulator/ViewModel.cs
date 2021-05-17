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

        public int SupplyRate { get; init; }
        public int DischargeRate { get; init; }

        public State CoolantCircut { get; init; }
        public State ProductValve { get; init; }

        public MixState MixState { get; init; }

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

        private Comm.Sender _sender = new Comm.Sender("127.0.0.1", 3000);
        List<DelegateCommand> commands = new List<DelegateCommand>();
        public void InvalidateCommands() => this.commands.ForEach(c => c.RaiseCanExecuteChanged());
        public ViewModel()
        {
            this.SetManualModeCommand = new DelegateCommand(
                executeHandler: async () => {
                    this.ProcessState = this.ProcessState with { H1Manual = State.On };
                    this.InvalidateCommands();
                },
                canExecuteHandler: () => this.ProcessState.H1Manual != State.On
            );
            this.commands.Add(this.SetManualModeCommand);

            this.NeutralizeCommand = new DelegateCommand(
                executeHandler: async () => {
                    this.InvalidateCommands();
                    await this.AddInertGas();
                    await this.TryWaitForFillAndMixProduct();
                    this.InvalidateCommands();
                },
                canExecuteHandler: () => 
                    this.ProcessState.H4InertGaz == State.Stopped
                    && this.ProcessState.H1Manual == State.On 
                    && this.ProcessState.CoolantCircut == State.Stopped
            );
            this.commands.Add(this.NeutralizeCommand);

            this.AddCatalistCommand = new DelegateCommand(
                executeHandler: () => this.AddCatalist(),
                canExecuteHandler: () => this.ProcessState.CoolantCircut == State.On && this.ProcessState.H3Catalist == State.Stopped
            );
            this.commands.Add(this.AddCatalistCommand);


            this.AddSubstraceCommand = new DelegateCommand(
                executeHandler: () => this.AddSubstrace(),
                canExecuteHandler: () => this.ProcessState.CoolantCircut == State.On && this.ProcessState.H3Catalist == State.Stopped
            );
            this.commands.Add(this.AddSubstraceCommand);
        }
        public void SendData()
        {
            if (_isSendingData)
            {
                _sender.Send(Convert.ToByte(_currentStateOfTheProcess));
            }
        }

        void AddCatalist()
        {
            this.ProcessState = this.ProcessState with
            {
                H3Catalist = State.On
            };
            this.InvalidateCommands();
        }

        void AddSubstrace()
        {
            this.ProcessState = this.ProcessState with
            {
                H2Substrace = State.On
            };
            this.InvalidateCommands();
        }
        async Task AddInertGas()
        {
            for (int i = 1; i < 10; i++)
            {
                this.ProcessState = this.ProcessState with { H4InertGaz = State.On };
                await Task.Delay(500);
                this.ProcessState = this.ProcessState with { H4InertGaz = State.Off };
                await Task.Delay(500);
            }
            this.ProcessState = this.ProcessState with
            {
                H4InertGaz = State.Stopped,
                CoolantCircut = State.On,
            };
            this.InvalidateCommands();
        }

        CancellationTokenSource cancelMixing = new CancellationTokenSource();

        async Task TryWaitForFillAndMixProduct()
        {
            try
            {
                await this.WaitForFillAndMixProduct(cancelMixing.Token);
            }
            catch (OperationCanceledException)
            {
                this.ProcessState = this.ProcessState with
                {
                    MixState = MixState.Stopped,
                    H2Substrace = State.Stopped,
                    H3Catalist = State.Stopped,
                    ProductValve = State.Stopped
                };
            }
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
            this.ProcessState = this.ProcessState with
            {
                CoolantCircut = State.Stopped,
                H2Substrace = State.Stopped,
                H3Catalist = State.Stopped,
                ProductValve = State.Stopped
            };
            this.InvalidateCommands();
        }

        //  de aici in jos este implementata partea de simulare a procesului
        #region Simulator

        private ProcessState _currentStateOfTheProcess = new ProcessState();
        public ProcessState ProcessState
        {
            get => _currentStateOfTheProcess;
            set => this.SetProperty(ref _currentStateOfTheProcess, value);
        }
        public DelegateCommand SetManualModeCommand { get; }
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
