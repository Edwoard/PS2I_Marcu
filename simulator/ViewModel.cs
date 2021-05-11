using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Simulator
{
    public enum ButtonState
    { 
        Off,
        Request,
        Blink,
        Flash,
    }

    public enum ProcessState
    {
        Off,
        BlinkOn,
        BlinkOff,     
        AutoRed,
        AutoYellow,
        AutoGreen,         
    }
    
    class ViewModel : INotifyPropertyChanged
    {

        // foarte important in cadrul aplicatiilor de tip WPF este ca in view model sa fie implementata interfata INotifyPropertyChanged
        // implementarea acestei interfete face ca atunci cand avem in codul xaml controale care fac bind pe anumite proprietati ale viewmodel-ului
        // sa fie actualizate automat atunci cand proprietatiile pe care se face bind sunt actualizate in view model.

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string prop)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }

        // acest flag este utilizat pentru a trimite date catre alte aplicatii, daca este setat pe true, iar daca este setat pe false face ca simulator sa functioneze ca o aplicatie simpla
        // care nu comunica cu alte aplicatii
        public bool _isSendingData = true;

        // backgroundworker-ul si timer-ul sunt folosite pentru a simula procesul adica pentru a realiza o tranzitie intre anumite stari la anumite perioade de timp
        private BackgroundWorker _worker = new BackgroundWorker();
        private System.Timers.Timer _timer = new System.Timers.Timer();
        
        private Comm.Sender _sender;
        private bool _setNextState = false;
        private ProcessState _nextState;

        public ViewModel() {}

        public void SendData()
        {
            if(_isSendingData)
            {
                _sender.Send(Convert.ToByte(_currentStateOfTheProcess));
            }
        }


        //  de aici in jos este implementata partea de simulare a procesului
        #region Simulator
        public void Init()
        {
            if(_isSendingData)
            {
                _sender = new Comm.Sender("127.0.0.1", 5500);
            }
            
            _timer.Elapsed += _timer_Elapsed;
            _worker.DoWork += _worker_DoWork;
            _worker.RunWorkerAsync();
        }

        private ProcessState _currentStateOfTheProcess = ProcessState.Off;
        public ProcessState CurrentStateOfTheProcess
        {
            get => _currentStateOfTheProcess;
            set
            {
                // urmatoarele doua linii de cod opresc timerul deoarece a avut loc o tranzitie de stare
                _setNextState = false;
                _timer.Stop();

                // dupa care se actualizeaza starea curenta si se trimit actualizarile de stare mai departe
                _currentStateOfTheProcess = value;
                SendData();
            }
        }

        private void _timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            CurrentStateOfTheProcess = _nextState;
        }

        private void RaiseTimerEvent(ProcessState NextStateOfTheProcess, int TimeInterval)
        {
            if(!_setNextState)
            {
                _setNextState = true;
                _nextState = NextStateOfTheProcess;
                _timer.Interval = TimeInterval;
                _timer.Start();
            }
        }

        private void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            // idea de baza a simulatorului este urmatoarea: backgroundworker-ul evalueza tot la 100 de milisecunde starea curenta a procesului 
            // si seteaza in viewmodel variabilele care actualizeaza UI-ul dupa care utilizand RaiseTimerEvent(NextProcessState, 2000) determina o tranzitie de stare peste un
            // interval de timp specificat de al doilea paramteru al acestei metode

            while (true)
            {              
                ProcessNextState(CurrentStateOfTheProcess);
                System.Threading.Thread.Sleep(100);
            }
        }

        // aceasta metoda proceseaza starea curenta, in funtie de valoarea acesteia seteaza anumite variabile care actualizeaza UI-ul 
        // si utilizand metoda RaiseTimerEvent(NextState, 2000) declanseaza o tranzitie viitoare de stare
        public void ProcessNextState(ProcessState CurrentState)
        {
            switch (CurrentState)
            {
                case ProcessState.Off:
                    IsRedForCar = false;
                    IsYellowForCar = false;
                    IsGreenForCar = false;
                    IsRedForPeople = false;
                    IsGreenForPeople = false;

                    RaiseTimerEvent(ProcessState.Off, 2000);
                   
                    break;
                case ProcessState.BlinkOn:
                    IsRedForCar = false;
                    IsYellowForCar = true;
                    IsGreenForCar = false;
                    IsRedForPeople = false;
                    IsGreenForPeople = false;

                    RaiseTimerEvent(ProcessState.BlinkOff, 2000);

                    break;
                case ProcessState.BlinkOff:
                    IsRedForCar = false;
                    IsYellowForCar = false;
                    IsGreenForCar = false;
                    IsRedForPeople = false;
                    IsGreenForPeople = false;
                    
                    RaiseTimerEvent(ProcessState.BlinkOn, 2000);
                   
                    break;               
                case ProcessState.AutoRed:
                    IsRedForCar = true;
                    IsYellowForCar = false;
                    IsGreenForCar = false;
                    IsRedForPeople = false;
                    IsGreenForPeople = true;

                    RaiseTimerEvent(ProcessState.AutoGreen, 5000);

                    break;
                case ProcessState.AutoYellow:
                    IsRedForCar = false;
                    IsYellowForCar = true;
                    IsGreenForCar = false;
                    IsRedForPeople = true;
                    IsGreenForPeople = false;
                    
                    RaiseTimerEvent(ProcessState.AutoRed, 3000);

                    break;
                case ProcessState.AutoGreen:
                    IsRedForCar = false;
                    IsYellowForCar = false;
                    IsGreenForCar = true;
                    IsRedForPeople = true;
                    IsGreenForPeople = false;

                    RaiseTimerEvent(ProcessState.AutoYellow, 10000);

                    break;                              
            }

        }

        // aceasta metoda este apelata direct de pe UI, pentru a forta o tranzitie de stare a procesului
        // tranzitia de stare forteaza oprirea timer-ului care ar trebui sa declanseze urmatoarea tranzitie de stare
        // si seteaza ca si stare curenta starea primita ca si parametru
        // dupa care backgroundworker-ul o sa se ocupe de tot si va declansa urmatoarea tranzitie de stare care urmeaza
        public void ForceNextState(ProcessState NextState)
        {
            CurrentStateOfTheProcess = NextState;
        }
        #endregion

        // de aici in jos sunt proprietatiile pe care le folosim pentru a actualiza UI-ul
        #region UI_updates
        private bool _isRedForCar;
        public bool IsRedForCar 
        {
            get
            {
                return _isRedForCar;
            }
            set
            {
                _isRedForCar = value;
                OnPropertyChanged(nameof(IsRedForCarsVisible));
            }
        }

        public System.Windows.Visibility IsRedForCarsVisible
        {
            get
            {
                if(_isRedForCar)
                {
                    return System.Windows.Visibility.Visible;
                }
                else
                {
                    return System.Windows.Visibility.Hidden;
                }
            }
        }

        private bool _isYellowForCar;
        public bool IsYellowForCar
        {
            get
            {
                return _isYellowForCar;
            }
            set
            {
                _isYellowForCar = value;
                OnPropertyChanged(nameof(IsYellowForCarsVisible));
            }
        }

        public System.Windows.Visibility IsYellowForCarsVisible
        {
            get
            {
                if (_isYellowForCar)
                {
                    return System.Windows.Visibility.Visible;
                }
                else
                {
                    return System.Windows.Visibility.Hidden;
                }
            }
        }

        private bool _isGreenForCar;
        public bool IsGreenForCar
        {
            get
            {
                return _isGreenForCar;
            }
            set
            {
                _isGreenForCar = value;
                OnPropertyChanged(nameof(IsGreenForCarsVisible));
            }
        }

        public System.Windows.Visibility IsGreenForCarsVisible
        {
            get
            {
                if (_isGreenForCar)
                {
                    return System.Windows.Visibility.Visible;
                }
                else
                {
                    return System.Windows.Visibility.Hidden;
                }
            }
        }

        private bool _isGreenForPeople;
        public bool IsGreenForPeople
        {
            get
            {
                return _isGreenForPeople;
            }
            set
            {
                _isGreenForPeople = value;
                OnPropertyChanged(nameof(IsGreenForPeopleVisible));
            }
        }

        public System.Windows.Visibility IsGreenForPeopleVisible
        {
            get
            {
                if (_isGreenForPeople)
                {
                    return System.Windows.Visibility.Visible;
                }
                else
                {
                    return System.Windows.Visibility.Hidden;
                }
            }
        }

        private bool _isRedForPeople;
        public bool IsRedForPeople
        {
            get
            {
                return _isRedForPeople;
            }
            set
            {
                _isRedForPeople = value;
                OnPropertyChanged(nameof(IsRedForPeopleVisible));
            }
        }

        public System.Windows.Visibility IsRedForPeopleVisible
        {
            get
            {
                if (_isRedForPeople)
                {
                    return System.Windows.Visibility.Visible;
                }
                else
                {
                    return System.Windows.Visibility.Hidden;
                }
            }
        }
        #endregion
    }
}
