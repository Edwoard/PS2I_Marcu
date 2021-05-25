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
    public enum State
    {
        // Oprit
        Off,
        // Pornit
        On
    }
    public enum MixState 
    {
        // Mixer oprit
        Off, 
        // Mixer se invarte in sensul acelor de ceasornic
        Clockwise,
        // Mixer se invarte in sensul contrar acelor de ceasornic
        CounterClockwise
    }

    // Clasa cae tine starea curenta a proiectului
    // Aceasta clasa va fi transmisa in format JSON catre serverul TCP
    public record ProcessState
    {
        public State H1Manual { get; init; }
        public State H2Substance { get; init; }
        public State H3Catalyst { get; init; }
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

        // Lista tuturor comenzilor din ViewMode
        List<DelegateCommand> commands = new List<DelegateCommand>();
        // Invalideaza toate comenzile pentru a activa/dezactiva butoanele asociate
        public void InvalidateCommands() => this.commands.ForEach(c => c.RaiseCanExecuteChanged());

        // Prop ajutatoare, este true daca modul manual sau auto sunt activate
        private bool IsManualOrAuto => this.ProcessState.H1Manual == State.On || this.ProcessState.H5AutoMode == State.On;
        public ViewModel()
        {
            // Comanda pentru simularea erorii de racire
            this.WaterCoolantErrorCommand = new DelegateCommand(
                executeHandler: () => this.WaterCoolantError(),
                canExecuteHandler: () => this.IsManualOrAuto // Se poate executa doar cand systemul este pornit
            );
            this.commands.Add(this.WaterCoolantErrorCommand);

            // Comanda pentru oprirea fortata a procesului 
            this.AbortCommand = new DelegateCommand(
                executeHandler: () => this.AbortProcess(),
                canExecuteHandler: () => this.cancelProcess != null // Se poate exeuta doar daca un proces este activ
            );
            this.commands.Add(this.AbortCommand);

            // Comanda pentru setarea modului manual 
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

            // Comanda pentru setarea modului auto
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

            // Comanda pentru pornirea procesului de reactie
            this.NeutralizeCommand = new DelegateCommand(
                executeHandler: () => this.currentProcessTask = this.TryStartProcess(),
                canExecuteHandler: () => 
                    this.ProcessState.H4InertGaz == State.Off
                    && this.IsManualOrAuto
                    && !this.ProcessState.IsNewtralised
            );
            this.commands.Add(this.NeutralizeCommand);

            // Comanda pentru deschiderea valvei care adauga catalistul 
            this.AddCatalistCommand = new DelegateCommand(
                executeHandler: () => this.AddCatalist(),
                // Poate fi executata doar dupa ce a fost rulat pasul de neutralizare
                // Si doar daca valva pentru catalist nu e deja deschisa
                canExecuteHandler: () => 
                    this.ProcessState.IsNewtralised
                    && this.ProcessState.H3Catalyst == State.Off
            );
            this.commands.Add(this.AddCatalistCommand);

            // Comanda pentru deschiderea valvei care adauga substanta
            this.AddSubstraceCommand = new DelegateCommand(
                executeHandler: () => this.AddSubstrace(),
                // Poate fi executata doar dupa ce a fost rulat pasul de neutralizare
                // Si doar daca valva pentru substanta nu e deja deschisa
                canExecuteHandler: () =>
                    this.ProcessState.IsNewtralised
                    && this.ProcessState.H2Substance == State.Off
            );
            this.commands.Add(this.AddSubstraceCommand);
        }

        ProcessState ResetProcessState()
        {
            return new ProcessState
            {
                DischargeRate = this.ProcessState.DischargeRate,
                SupplyRate = this.ProcessState.SupplyRate,
            };
        }
        /// <summary>
        /// Seteaza campul care marcheaza faptul ca valva pentru catalist este deschisa
        /// </summary>
        void AddCatalist()
        {
            this.ProcessState = this.ProcessState with
            {
                H3Catalyst = State.On
            };
        }


        /// <summary>
        /// Seteaza campul care marcheaza faptul ca valva pentru catalist este deschisa
        /// </summary>
        void AddSubstrace()
        {
            this.ProcessState = this.ProcessState with
            {
                H2Substance = State.On
            };
        }


        /// <summary>
        /// Trece prin procesul de adugare a gazului inert
        /// </summary>
        async Task AddInertGas(CancellationToken cancellationToken)
        {
            // Executa 10 repetitii in care:
            for (int i = 1; i < 10; i++)
            {
                // Verifica daca s-a cerut oprirea procesului
                cancellationToken.ThrowIfCancellationRequested();
                // Aprinde ledul de gaz inert
                this.ProcessState = this.ProcessState with { H4InertGaz = State.On };
                // Asteapta 0.5 secunde
                await Task.Delay(500);
                // Verifica daca s-a cerut oprirea procesului
                cancellationToken.ThrowIfCancellationRequested();
                // Stinge ledul de gaz inert
                this.ProcessState = this.ProcessState with { H4InertGaz = State.Off };
                // Asteapta 0.5 secunde
                await Task.Delay(500);
            }
            // Verifica daca s-a cerut oprirea procesului
            cancellationToken.ThrowIfCancellationRequested();

            // Porneste circuitul de racire cu apa si marcheaza pasul de neutralizare ca fiind finalizat
            this.ProcessState = this.ProcessState with
            {
                H4InertGaz = State.Off,
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
                CoolantCircut = State.Off
            };
        }
        void AbortProcess()
        {
            cancelProcess?.Cancel();
        }
        Task currentProcessTask;
        /// <summary>
        /// Porneste procesul, cu optiunea de a-l opri prin folosirea lui cancelProcess
        /// </summary>
        /// <returns></returns>
        async Task TryStartProcess()
        {
            try
            {
                // Seteaza cancelProcess ca sa putem sa oprim procesul daca userul cere acest lucru
                cancelProcess = new CancellationTokenSource();
                // Executam procesul de adaugare a gazului inert
                await this.AddInertGas(cancelProcess.Token);
                // Daca suntem in modul auto, deschidem automat si valvele pt catalist si substranta
                if(this.ProcessState.H5AutoMode == State.On)
                {
                    this.AddCatalist();
                    this.AddSubstrace();
                }
                // Asteptam L+, si apoi executam amestecarea si eliberarea produsului
                await this.WaitForFillAndMixProduct(cancelProcess.Token);
            }
            catch (OperationCanceledException)
            {
                // Daca operatia a fost oprita de user, resetam starea procesului,
                // dar lasam circuitul de racire pornit, conform cu cerinta
                this.ProcessState = this.ResetProcessState() with
                {
                    CoolantCircut = State.On,
                };
            }
            // Procesul s-a finalizat, nu mai avem ce sa anulam.
            cancelProcess = null;
        }
        async Task WaitForFillAndMixProduct(CancellationToken cancellationToken)
        {
            // Asteptam pana cand senzorul LPlus semanaleaza umplerea reactorului
            while (!this.ProcessState.LPlus)
            {
                await Task.Delay(1);
                cancellationToken.ThrowIfCancellationRequested();
            }

            // Executam doua cicluri de amestecare in sensul si contra sensul acelor de ceasornic
            // 2*(2sec + 2sec) = 8sec
            for (int i = 0; i < 2; i++)
            {
                // Marcam faptul ca se amesteca in sensul acelor de ceasornic
                this.ProcessState = this.ProcessState with
                {
                    MixState = MixState.Clockwise
                };
                // Lasam starea de amestecare pt 2 secunde
                await Task.Delay(2000);

                // Verificam daca s-a cerut oprirea procesului
                cancellationToken.ThrowIfCancellationRequested();
                // Marcam faptul ca se amesteca in contrar sensul acelor de ceasornic
                this.ProcessState = this.ProcessState with
                {
                    MixState = MixState.CounterClockwise
                };
                // Lasam starea de amestecare pt 2 secunde
                await Task.Delay(2000);
                // Verificam daca s-a cerut oprirea procesului
                cancellationToken.ThrowIfCancellationRequested();
            }
            // Mai apestecam o data pentru a ajunge la cele 10 secunde (8secunde din for + 2 secunde acum)
            this.ProcessState = this.ProcessState with
            {
                MixState = MixState.Clockwise
            };
            // Lasam starea de amestecare pt 2 secunde
            await Task.Delay(2000);
            // Verificam daca s-a cerut oprirea procesului
            cancellationToken.ThrowIfCancellationRequested();

            // Resetam sensoorul de umplere, 
            // Oprim amestecarea
            // Deschidem valva de evacuare a produsului finit
            this.ProcessState = this.ProcessState with
            {
                LPlus = false,
                MixState = MixState.Off,
                ProductValve = State.On
            };

            // Asteptam ca senzorul LMinus sa semnaleze golirea reactorului 
            while (!this.ProcessState.LMinus)
            {
                await Task.Delay(1);
                cancellationToken.ThrowIfCancellationRequested();
            }
            // Resetam starea procesului, pt a putea incepe din nou
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
                // Setam nou a stare a procesului 
                this.SetProperty(ref _currentStateOfTheProcess, value);
                // Invalidam toate comenzile, majoritatea depinde de stare oricum
                this.InvalidateCommands();
                // Notificam ca potential s-au modificat unele prop care depind de stare
                this.OnPropertyChanged(nameof(LPlus));
                this.OnPropertyChanged(nameof(LMinus));
                this.OnPropertyChanged(nameof(DischargeRate));
                this.OnPropertyChanged(nameof(SupplyRate));
                // Trimitem starea procesului la serverul C{
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
            set => this.ProcessState = this.ProcessState with { LPlus = value, LMinus = false };
        }

        public bool LMinus
        {
            get => this.ProcessState.LMinus;
            set => this.ProcessState = this.ProcessState with { LMinus = value, LPlus = false };
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

    // Clasa care repreznta o comanda
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
