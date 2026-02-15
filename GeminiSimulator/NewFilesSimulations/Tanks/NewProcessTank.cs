using GeminiSimulator.Materials;
using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.ManufactureEquipments;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.PackageLines;
using QWENShared.Enums;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.Tanks
{
    public class NewProcessTank : NewPlantUnit
    {

        protected const double MASS_TOLERANCE = 0.01;
        public Amount Capacity { get; private set; }
        public Amount HiLevelAlarm { get; private set; }
        public Amount HiLevelControl { get; private set; }
        public Amount LoLevelControl { get; private set; }
        public Amount LoLevelAlarm { get; private set; }

        protected double _currentLevel;
        public Amount CurrentLevel => new Amount(_currentLevel, MassUnits.KiloGram);

        protected double _totalOutletMass;
        public Amount TotalOutletMass => new Amount(_totalOutletMass, MassUnits.KiloGram);
        protected double _AverageOutletFlow => SecondsAvailable == 0 ? 0 : _totalOutletMass / SecondsAvailable; // Promedio por minuto
        public Amount AverageOutletFlow => new Amount(_AverageOutletFlow, MassFlowUnits.Kg_sg);
        protected double _timeToEmptyCurrentLevel => _AverageOutletFlow == 0 ? 0 : _currentLevel / _AverageOutletFlow;

        public Amount TimeToEmptyCurrentLevel => new Amount(_timeToEmptyCurrentLevel, TimeUnits.Second);

        protected List<NewPump> _OutletPumps => Outputs.OfType<NewPump>().ToList();
        protected List<NewPump> _InletPumps => Inputs.OfType<NewPump>().ToList();

        protected double CurrentOutletFlow = 0;
        public virtual void SetOutletFlow()
        {
            CurrentOutletFlow = _OutletPumps.Sum(x => x.CurrentFlow.GetValue(MassFlowUnits.Kg_sg));


            _totalOutletMass += CurrentOutletFlow;
            _currentLevel -= CurrentOutletFlow;

        }
        public virtual void SetInletFlow(double inletflow)
        {
            _currentLevel += inletflow;
        }

        public NewProcessTank(Guid id, string name, ProcessEquipmentType type, FocusFactory factory,
            Amount _Capacity,
            Amount _HiLevelAlarm,
            Amount _HiLevelControl,
            Amount _LolevelControl,
            Amount _LoLevelAlarm,
            Amount _InitialLevel
            ) : base(id, name, type, factory)
        {
            Capacity = _Capacity;
            HiLevelAlarm = _HiLevelAlarm;
            HiLevelControl = _HiLevelControl;
            LoLevelControl = _LolevelControl;
            LoLevelAlarm = _LoLevelAlarm;
            _currentLevel = _InitialLevel.GetValue(MassUnits.KiloGram);
        }
        protected override void AddSpecificReportData(List<NewInstantaneousReport> reportList)
        {
            // DEFINICIÓN ESTRICTA DE COLORES
            string neutral = ""; // Negro
            string alert = "#F44336";   // Rojo

            // 1. NIVEL
            // Lógica: Si está fuera de rango (Alarma) -> Rojo. Si está OK -> NEGRO.
            bool isAlarm = _currentLevel < LoLevelAlarm.GetValue(MassUnits.KiloGram) ||
                           _currentLevel > HiLevelAlarm.GetValue(MassUnits.KiloGram);

            string levelColor = isAlarm ? alert : neutral;

            double pct = Capacity.GetValue(MassUnits.KiloGram) == 0 ? 0 :
                (_currentLevel / Capacity.GetValue(MassUnits.KiloGram)) * 100;
            if (CurrentMaterial != null)
            {
                reportList.Add(new NewInstantaneousReport("Material", CurrentMaterial.Name, IsBold: false, Color: neutral));
            }
            reportList.Add(new NewInstantaneousReport("Level",
                $"{CurrentLevel.ToString()} ({pct:F0}%)",
                IsBold: isAlarm, // Negrita solo si es alarma
                Color: levelColor)); // ¡AQUÍ ESTABA EL ERROR DEL AZUL!

            // 2. BOMBAS (Todo Negro)
            var pumps = Outputs.OfType<NewPump>().ToList();
            if (pumps.Any())
            {
                reportList.Add(new NewInstantaneousReport("--- PUMPS ---", "", Color: neutral));
                foreach (var pump in pumps)
                {
                    string pumpStatus = pump.CurrentOwner == null ? "Idle" : $"In Use by {pump.CurrentOwner.Name}";
                    reportList.Add(new NewInstantaneousReport(pump.Name, pumpStatus, Color: neutral));
                }
            }
        }
        public override void CheckInitialStatus(DateTime initialDate)
        {
            base.CheckInitialStatus(initialDate); // Esto nos pone en GlobalState_Operational por defecto

            // Chequeo inicial de nivel físico
            if (_currentLevel < LoLevelAlarm.GetValue(MassUnits.KiloGram))
            {
                // 1. Me pongo yo en Estado de Alarma (Outlet)
                var alarmState = new NewTankLowLevelOutletState(this);
                TransitionOutletState(alarmState);

                // 2. BLOQUEO DE ESCLAVOS (Nueva Arquitectura)
                // Como estamos iniciando, asumimos autoridad total sobre las bombas de salida.
                // Las ponemos en 'SlaveBlocked' para que no arranquen en seco.
                foreach (var output in _OutletPumps)
                {
                    output.TransitionGlobalState(
                        new GlobalState_SlaveBlocked(output, this, "Initial Low Level") // Etiqueta clara
                    );
                }
                return;
            }

            // Si el nivel está bien, arrancamos normal
            TransitionOutletState(new NewTankNormalOutletState(this));
        }
    }
    // Estado: Nivel Normal (Todo fluye)
    // Estado: Nivel Normal
    public class NewTankNormalOutletState : INewOutletState
    {
        public string SubStateName => string.Empty;
        private readonly NewProcessTank _unit;
        public NewTankNormalOutletState(NewProcessTank unit) => _unit = unit;
        public string StateLabel => "Level OK";
        public string HexColor => "#00C853";
        public bool IsProductive => true;
        public void Calculate()
        {

            _unit.SetOutletFlow();
        }


        public void CheckTransitions()
        {
            // Si baja el nivel...
            if (_unit.CurrentLevel < _unit.LoLevelAlarm)
            {
                var newState = new NewTankLowLevelOutletState(_unit);
                _unit.TransitionOutletState(newState);

                // LÓGICA DE BLOQUEO FÍSICO (PUSH)
                // No "liberamos" la bomba (porque no es nuestra), 
                // la "bloqueamos" (SlaveBlocked) avisando que es por Nivel Bajo.
                foreach (var output in _unit.Outputs)
                {
                    // Solo afectamos a las bombas que estaban sanas
                    if (output.GlobalState.IsOperational)
                    {
                        // USAMOS LA NUEVA ARQUITECTURA:
                        output.TransitionGlobalState(
                            new GlobalState_SlaveBlocked(output, _unit, "Tank Low Level")
                        );
                        if (output.CurrentOwner != null && output.CurrentOwner.GlobalState.IsOperational)
                        {
                            output.CurrentOwner.TransitionGlobalState(new GlobalState_SlaveBlocked(output.CurrentOwner, output, "Tank Low Level"));

                        }

                    }
                }
            }
        }
    }

    // Estado: Nivel Bajo (Alarma)
    public class NewTankLowLevelOutletState : INewOutletState
    {
        private readonly NewProcessTank _unit;
        public NewTankLowLevelOutletState(NewProcessTank unit) => _unit = unit;

        public string StateLabel => "Low Level Alarm";
        public string SubStateName => string.Empty;
        public string HexColor => "#F44336"; // Rojo Alarma
        public bool IsProductive => false;

        public void Calculate()
        {
            _unit.SetOutletFlow();
        }

        public void CheckTransitions()
        {
            // 1. CONDICIÓN DE RECUPERACIÓN
            // (Tip Pro: Aquí podrías usar 'LoLevelControl' en vez de 'LoLevelAlarm' 
            // para tener Histéresis y evitar que parpadee si el nivel oscila).
            if (_unit.CurrentLevel.GetValue(MassUnits.KiloGram) >= _unit.LoLevelControl.GetValue(MassUnits.KiloGram))
            {
                // 2. EL TANQUE VUELVE A LA NORMALIDAD
                _unit.TransitionOutletState(new NewTankNormalOutletState(_unit));

                // 3. LÓGICA PUSH: DESPERTAR A LAS BOMBAS
                foreach (var output in _unit.Outputs)
                {
                    // Intentamos liberar pasando nuestra identidad
                    output.TryReleaseFromBlock(_unit);
                    if (output.CurrentOwner != null)
                    {
                        output.CurrentOwner.TryReleaseFromBlock(output);
                    }
                }
            }
        }
    }
    public class NewRawMaterialTank : NewProcessTank
    {
        public NewRawMaterialTank(Guid id, string name, ProcessEquipmentType type, FocusFactory factory,
            Amount _Capacity,
            Amount _HiLevelAlarm,
            Amount _HiLevelControl,
            Amount _LolevelControl,
            Amount _LoLevelAlarm,
            Amount _InitialLevel
            ) : base(id, name, type, factory,
                  _Capacity,
                  _HiLevelAlarm,
                  _HiLevelControl,
                  _LolevelControl,
                  _LoLevelAlarm,
                  _InitialLevel)
        {
        }
        public override void CheckInitialStatus(DateTime initialDate)
        {
            base.CheckInitialStatus(initialDate);
            if (SupportedProducts.Any())
            {
                CurrentMaterial = SupportedProducts.First();
            }
            TransitionInletState(new NewRawMaterialTankNormalInletState(this));
        }
    }
    public class NewRawMaterialTankNormalInletState : INewInletState
    {
        private readonly NewRawMaterialTank _unit;
        public NewRawMaterialTankNormalInletState(NewRawMaterialTank unit) => _unit = unit;
        public string StateLabel => "Level OK";
        public string HexColor => "#00C853";
        public bool IsProductive => true;
        public void Calculate() { }


        public void CheckTransitions()
        {
            // Si baja el nivel...
            if (_unit.CurrentLevel < _unit.LoLevelControl)
            {
                _unit.SetInletFlow((_unit.Capacity - _unit.CurrentLevel).GetValue(MassUnits.KiloGram));
            }
        }
    }

    // Estado: Nivel Bajo (Alarma)
    public class NewRawMaterialInhouseTank : NewRecipedInletTank
    {

        public NewRawMaterialInhouseTank(Guid id, string name, ProcessEquipmentType type, FocusFactory factory,
         Amount _Capacity,
         Amount _HiLevelAlarm,
         Amount _HiLevelControl,
         Amount _LolevelControl,
         Amount _LoLevelAlarm,
         Amount _InitialLevel
         ) : base(id, name, type, factory,
               _Capacity,
               _HiLevelAlarm,
               _HiLevelControl,
               _LolevelControl,
               _LoLevelAlarm,
               _InitialLevel)
        {
        }
        public override void CheckInitialStatus(DateTime initialDate)
        {
            base.CheckInitialStatus(initialDate);
            if (SupportedProducts.Any())
            {
                CurrentMaterial = SupportedProducts.First();
            }

        }
    }
    public class NewWipTank : NewRecipedInletTank
    {
        //Tanque espcializado para enviar producto a lineas de empaque
        public NewWipTank(Guid id, string name, ProcessEquipmentType type, FocusFactory factory,
         Amount _Capacity,
         Amount _HiLevelAlarm,
         Amount _HiLevelControl,
         Amount _LolevelControl,
         Amount _LoLevelAlarm,
         Amount _InitialLevel
         ) : base(id, name, type, factory,
               _Capacity,
               _HiLevelAlarm,
               _HiLevelControl,
               _LolevelControl,
               _LoLevelAlarm,
               _InitialLevel)
        {
        }
        public override void CheckInitialStatus(DateTime initialDate)
        {
            base.CheckInitialStatus(initialDate);

        }
        public double _massRequiredFromLine;
        public double _massRemainingToSendToLine => _massRequiredFromLine - _massSentToLine;
        public double _massSentToLine;

        public Amount MassPendingToSendToLine=>new Amount(_massRemainingToSendToLine,MassUnits.KiloGram);
        public Amount MassSentToLine => new Amount(_massSentToLine, MassUnits.KiloGram);
        public override void SetOutletFlow()
        {
            base.SetOutletFlow();
            _massSentToLine += CurrentOutletFlow;
        }
        public NewLine? CurrentLine { get; set; }
        public string CurrentLineName => CurrentLine?.Name ?? string.Empty;
        public void ReceiveOrderFromProductionLine(NewLine _CurrentLine, ProductDefinition product, double _MassToPack)
        {
            CurrentLine = _CurrentLine;
            CurrentMaterial = product;

            // LÓGICA VIEJA
            _massRequiredFromLine = _MassToPack;
            _massSentToLine = 0;
            // (Lógica de cálculo de _massRequiredFromManufacture...)

            _massRequiredFromManufacture = _MassToPack - _currentLevel;
            // --- NUEVA LÓGICA (Inicialización del Contrato) ---
            _massReceivedFromManufacture = 0;

            // Si ya hay stock, la "Promesa" inicial es 0, 
            // y el NewBalanceToOrder dirá que solo falta fabricar la diferencia.
        }

        public void SetCoutersToZero()
        {
            // LÓGICA VIEJA
            _massRequiredFromLine = 0;
            _massSentToLine = 0;
            _massReceivedFromManufacture = 0;
            _massRequiredFromManufacture = 0;


            _currentLevel = 0;
        }
        // En NewRecipedInletTank.cs (o NewWipTank)

        protected override void AddSpecificReportData(List<NewInstantaneousReport> reportList)
        {
            string neutral = ""; // Negro estricto

            // 1. Llamamos a la base (Traerá el Nivel en Negro/Rojo)
            base.AddSpecificReportData(reportList);

            // 2. PRODUCTO (Estaba saliendo azul/morado)
           
            reportList.Add(new NewInstantaneousReport("Mass Pending To Send to Line", MassPendingToSendToLine.ToString(), IsBold: false, Color: neutral));
            reportList.Add(new NewInstantaneousReport("Mass Pending From Manufacture", MassPendingToReceiveFromManufacture.ToString(), IsBold: false, Color: neutral));




            // 5. DESTINO
            var connectedLine = Outputs.OfType<NewLine>().FirstOrDefault();
            if (connectedLine != null)
            {
                reportList.Add(new NewInstantaneousReport("Feeding", $"-> {connectedLine.Name}", Color: neutral));
            }
        }
    }
    public class NewRecipedInletTank : NewProcessTank
    {
        protected double _massRequiredFromManufacture;
        protected double _massReceivedFromManufacture;
        protected double _massInManufacturePendingToReceive;





        public NewMixer? BestCandidate { get; set; }
        public NewMixer? AssignedMixer { get; set; }
        public Queue<NewMixer> MixerQueue { get; set; } = new();
        public Queue<BatchOrder> ProcesedBatchQueue { get; set; } = new();
        public virtual void AddMassInProcess(NewMixer _mixer, double massinprocess)
        {
            MixerQueue.Enqueue(_mixer);

            // LÓGICA VIEJA
            _massInManufacturePendingToReceive += massinprocess;

            // --- NUEVA LÓGICA ---

        }
        public Amount MassInManufacturePendingToReceive => new Amount(_massInManufacturePendingToReceive, MassUnits.KiloGram);

        // 2. Lo que ya llegó (Received)
        public Amount MassReceivedFromManufacture => new Amount(_massReceivedFromManufacture, MassUnits.KiloGram);

        // 3. El total de la orden (Required)
        public Amount MassRequiredFromManufacture => new Amount(_massRequiredFromManufacture, MassUnits.KiloGram);

        // 4. EL REAL REMANENTE (La Resta)
        // Exponemos el resultado del cálculo protected
        public Amount MassPendingToReceiveFromManufacture => new Amount(_massPendingToReceiveFromManufacture, MassUnits.KiloGram);

        // Cálculo protegido contra negativos visuales
        protected double _massPendingToReceiveFromManufacture
        {
            get
            {
                double remaining = _massRequiredFromManufacture - _massReceivedFromManufacture - _massInManufacturePendingToReceive;
                return remaining < MASS_TOLERANCE ? 0 : remaining;
            }
        }

        // Visión Global (Físico + Lo que viene en camino)
        protected double _totalMassInProcess => _currentLevel + _massInManufacturePendingToReceive;
        public Amount TotalMassInProcess => new Amount(_totalMassInProcess, MassUnits.KiloGram);

        // KPI: Tiempo para quedarse seco considerando lo que viene en camino
        protected double _pendingTimeEmptyMassInProcess =>
            (_AverageOutletFlow == 0)
            ? 0
            : _totalMassInProcess / _AverageOutletFlow;

        public Amount PendingTimeEmptyMassInProcess => new Amount(_pendingTimeEmptyMassInProcess, TimeUnits.Second);

        // Propiedad Helper para el Scheduler (Sin depender de lógica externa)

        public NewRecipedInletTank(Guid id, string name, ProcessEquipmentType type, FocusFactory factory,
         Amount _Capacity,
         Amount _HiLevelAlarm,
         Amount _HiLevelControl,
         Amount _LolevelControl,
         Amount _LoLevelAlarm,
         Amount _InitialLevel
         ) : base(id, name, type, factory,
               _Capacity,
               _HiLevelAlarm,
               _HiLevelControl,
               _LolevelControl,
               _LoLevelAlarm,
               _InitialLevel)
        {
        }
        public override void CheckInitialStatus(DateTime initialDate)
        {
            base.CheckInitialStatus(initialDate);
            TransitionInletState(new NewInletRecipedTankAvailableState(this));
        }
        public override void SetInletFlow(double inletflow)
        {
            // A. Física de nivel (Base)
            base.SetInletFlow(inletflow);

            if (CurrentOwner != null)
            {
                // B. LÓGICA VIEJA (Riesgo de doble resta detectado)
                _massReceivedFromManufacture += inletflow;

               

                if (CurrentOwner is NewMixer)
                {
                    _massInManufacturePendingToReceive -= inletflow;
                }
            }
        }
        public void ReceiveStopDischargFromMixer()
        {
      
            AssignedMixer = null!;
            TransitionInletState(new NewInletRecipedTankAvailableState(this));
        }

        // NUEVO: Método para empalmar órdenes (Hot Swap)
       
        

        // --- 4. REPORTES ESPECÍFICOS (La parte que faltaba) ---
        protected override void AddSpecificReportData(List<NewInstantaneousReport> reportList)
        {
            string neutral = ""; // Negro estricto

            // 1. Llamamos a la base (Traerá el Nivel en Negro/Rojo)
            base.AddSpecificReportData(reportList);

            // 2. PRODUCTO (Estaba saliendo azul/morado)
            

            // 3. DATOS DE PROCESO (Estaban saliendo en azul #2196F3)
            // Forzamos 'neutral' en todos:
            reportList.Add(new NewInstantaneousReport("Mass in Process", TotalMassInProcess.ToString(), IsBold: true, Color: neutral));
           

            reportList.Add(new NewInstantaneousReport("Time to Empty Level",
                $"{Math.Round(TimeToEmptyCurrentLevel.GetValue(TimeUnits.Minute), 1)}, min",
                IsBold: true, Color: neutral));

            reportList.Add(new NewInstantaneousReport("Time to Empty Mass in Process",
                $"{Math.Round(PendingTimeEmptyMassInProcess.GetValue(TimeUnits.Minute), 1)}, min",
                IsBold: true, Color: neutral));

            // 4. DATOS DE ORDEN (In Transit, Incoming, etc.)
            if (_massInManufacturePendingToReceive > 0.01) // Usando tolerancia simple
            {
                reportList.Add(new NewInstantaneousReport("Incoming", $"+{_massInManufacturePendingToReceive:F1} kg", Color: neutral));

                if (CurrentOwner != null)
                {
                    reportList.Add(new NewInstantaneousReport("From", CurrentOwner.Name, FontSize: "0.75rem", Color: neutral));
                }
            }

            // 5. DESTINO
          
        }

    }
    public class NewInletRecipedTankAvailableState : INewInletState
    {
        private readonly NewRecipedInletTank _unit;
        public NewInletRecipedTankAvailableState(NewRecipedInletTank unit) => _unit = unit;

        public string StateLabel => "Available";
        public string HexColor => "#00C853";
        public bool IsProductive => true;
        public void Calculate() { }

        public void CheckTransitions()
        {
            // --- LA LÓGICA FALTANTE ---

            // El tanque está vigilando: "¿Alguien me capturó para llenarme?"
            // Si CurrentOwner es un equipo de Manufactura (Skid/Mixer), significa 
            // que ya empezó la transacción.
            if (_unit.CurrentOwner is NewManufacture)
            {
                // ¡Cambio de modo! Ahora soy un tanque receptor activo.
                _unit.TransitionInletState(new NewRecipedTankReceivingState(_unit));
            }
        }
    }

    public class NewRecipedTankReceivingState : INewInletState
    {
        private readonly NewRecipedInletTank _tank;

        public NewRecipedTankReceivingState(NewRecipedInletTank tank)
        {
            _tank = tank;
        }

        public bool IsProductive => true; // Sí, está recibiendo líquido activamente
        public string StateLabel => "Receiving Product";
        public string HexColor => "#2196F3"; // Azul

        public void Calculate() { }

        public void CheckTransitions()
        {
            // LÓGICA PARA SKID (Flujo Continuo)
            if (_tank.CurrentOwner is NewSkid skid)
            {
                // VIEJO: var total = _tank.MassRequired + _tank.LoLevelAlarm;
                var total = _tank.MassPendingToReceiveFromManufacture + _tank.LoLevelAlarm;
                // --- NUEVO (Usa el balance limpio) ---
                if (total.GetValue(MassUnits.KiloGram) <= 0.01 || _tank.CurrentLevel >= _tank.HiLevelControl)
                {
                    skid.ReceiveStopCommand();
                    _tank.TransitionInletState(new NewInletRecipedTankAvailableState(_tank));
                    return;
                }
            }

            // LÓGICA PARA MIXER (Baches)
            if (_tank.CurrentOwner is NewMixer mixer)
            {
                if (_tank.CurrentLevel >= _tank.HiLevelControl)
                {
                    // Bloqueo por nivel físico (Hysteresis)
                    _tank.TransitionInletState(new NewRecipedTankForMixerFullState(_tank));
                }
            }
        }





    }
    public class NewRecipedTankForMixerFullState : INewInletState
    {
        private readonly NewRecipedInletTank _tank;

        public NewRecipedTankForMixerFullState(NewRecipedInletTank tank)
        {
            _tank = tank;
        }

        // CONCEPTUALMENTE: No es productivo porque está bloqueando al equipo de atrás.
        // Es un "Cuello de Botella" temporal.
        public bool IsProductive => false;

        public string StateLabel => "Full - Holding Mixer";

        // COLOR: Ámbar Oscuro / Naranja Fuerte (Indica "Precaución/Espera")
        public string HexColor => "#FF6F00";

        public void Calculate() { }

        public void CheckTransitions()
        {
            if (_tank.CurrentOwner is NewMixer mixer)
            {
                // 1. DATOS DEL TANQUE
                double currentLevel = _tank.CurrentLevel.GetValue(MassUnits.KiloGram);
                double capacity = _tank.Capacity.GetValue(MassUnits.KiloGram);
                double loControl = _tank.LoLevelControl.GetValue(MassUnits.KiloGram);

                // Espacio disponible actualmente
                double emptySpace = capacity - currentLevel;

                // 2. DATOS DEL MIXER
                double mixerRemainingMass = mixer.CurrentLevel.GetValue(MassUnits.KiloGram);

                // 3. LA LÓGICA HÍBRIDA (TU IDEA):
                // Condición A: "Vaciado Total" -> Si cabe todo lo que queda, dale. (Libera al Mixer rápido).
                // Condición B: "Relleno de Emergencia" -> Si el tanque bajó de su nivel de control, dale. (Protege a la Línea).

                if (emptySpace >= mixerRemainingMass || currentLevel < loControl)
                {
                    // ¡ACCIÓN! SE CUMPLIÓ UNA DE LAS DOS.

                    // A. Despertamos al Mixer (Quitamos la espera)
                    // Usamos la Nueva Arquitectura: Solo si está en MasterWaiting (Naranja).
                    if (mixer.GlobalState is GlobalState_MasterWaiting)
                    {
                        mixer.TransitionGlobalState(new GlobalState_Operational(mixer));
                    }

                    // B. El tanque vuelve a modo "Recibiendo" para aceptar el flujo
                    _tank.TransitionInletState(new NewRecipedTankReceivingState(_tank));
                }
            }
        }
    }

}

