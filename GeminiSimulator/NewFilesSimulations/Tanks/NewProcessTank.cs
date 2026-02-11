using GeminiSimulator.Materials;
using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.ManufactureEquipments;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.PackageLines;
using QWENShared.Enums;
using System;
using System.Collections.Generic;
using System.Text;
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


        public void SetOutletMassCounterToZero()
        {
            _totalOutletMass = 0;
        }
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
            // 1. NIVEL (Lo más importante)
            // Calculamos porcentaje visual
            double pct = Capacity.GetValue(MassUnits.KiloGram) == 0 ? 0 :
                (_currentLevel / Capacity.GetValue(MassUnits.KiloGram)) * 100;

            string colorLevel = pct < 10 ? "#F44336" : "#2196F3"; // Rojo si está vacío

            reportList.Add(new NewInstantaneousReport("Level",
                $"{CurrentLevel.ToString()} ({pct:F0}%)",
                IsBold: true, Color: colorLevel));

            // 2. BOMBAS DE SALIDA
            // Mostramos cuáles están conectadas y su estado
            var pumps = Outputs.OfType<NewPump>().ToList();
            if (pumps.Any())
            {
                reportList.Add(new NewInstantaneousReport("--- PUMPS ---", "", Color: "#9E9E9E"));
                foreach (var pump in pumps)
                {

                    string pumpStatus = pump.CurrentOwner == null ? "Idle" : $"In Use by {pump.CurrentOwner.Name}";
                    string pumpColor = pumpStatus == "Idle" ? "#BDBDBD" : "#4CAF50";

                    reportList.Add(new NewInstantaneousReport(pump.Name, pumpStatus, Color: pumpColor));
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


        public void SetCoutersToZero()
        {
            _massRequiredFromLine = 0;
            _massSentToLine = 0;
            _massReceivedFromManufacture = 0;
            _massInManufacturePendingToReceive = 0;
            _massRequiredFromManufacture = 0;
            _currentLevel = 0;

        }
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
            // 1. Configuramos el producto
            CurrentMaterial = product;

            // 2. Registramos la demanda total de la línea
            _massRequiredFromLine = _MassToPack;
            _massSentToLine = 0; // Reseteamos contador de salida

            // 3. Calculamos cuánto hay que pedirle a Manufactura
            // Lógica correcta: Lo que necesito que fabriquen = Total Pedido - Lo que ya tengo
            double currentStock = CurrentLevel.GetValue(MassUnits.KiloGram);

            if (currentStock >= _MassToPack)
            {
                // Si ya tengo más de lo que piden, no necesito fabricar nada
                _massRequiredFromManufacture = 0;
            }
            else
            {
                // Solo fabrico la diferencia
                _massRequiredFromManufacture = _MassToPack - currentStock;
            }

            // 4. Reseteamos los contadores de progreso de manufactura
            _massReceivedFromManufacture = 0;
            _massInManufacturePendingToReceive = 0;
        }
        // En NewRecipedInletTank.cs (o NewWipTank)

        protected override void AddSpecificReportData(List<NewInstantaneousReport> reportList)
        {
            // 1. PRODUCTO (Si está asignado)
            if (CurrentMaterial != null)
            {
                reportList.Add(new NewInstantaneousReport("Product", CurrentMaterial.Name, IsBold: true, Color: "#3F51B5"));
            }

            // 2. NIVEL FÍSICO
            reportList.Add(new NewInstantaneousReport("Level", CurrentLevel.ToString(), IsBold: true, Color: "#2196F3"));
            reportList.Add(new NewInstantaneousReport("Mass in Process", MassInProcess.ToString(), IsBold: true, Color: "#2196F3"));
            reportList.Add(new NewInstantaneousReport("Time to Empty Level", $"{Math.Round(TimeToEmptyCurrentLevel.GetValue(TimeUnits.Minute), 1)}, min", IsBold: true, Color: "#2196F3"));
            reportList.Add(new NewInstantaneousReport("Time to Empty Mass in Process", $"{Math.Round(PendingTimeEmptyMassInProcess.GetValue(TimeUnits.Minute), 1)}, min", IsBold: true, Color: "#2196F3"));

            // 3. LO QUE VIENE EN CAMINO (Vital para el Scheduler)
            if (_massInManufacturePendingToReceive > 0)
            {
                reportList.Add(new NewInstantaneousReport("Incoming", $"+{_massInManufacturePendingToReceive:F1} kg", Color: "#FFC107")); // Ámbar

                // Identificar quién me lo manda (Dueño actual)
                if (CurrentOwner != null)
                {
                    reportList.Add(new NewInstantaneousReport("From", CurrentOwner.Name, FontSize: "0.75rem"));
                }
            }

            // 4. DESTINO (Línea de Empaque)
            // Buscamos a qué línea estamos conectados
            var connectedLine = Outputs.OfType<NewLine>().FirstOrDefault();
            if (connectedLine != null)
            {
                reportList.Add(new NewInstantaneousReport("Feeding", $"-> {connectedLine.Name}", Color: "#4CAF50"));
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
        public Queue<NewMixer> BatchQueue { get; set; } = new();
        public void AddMassInProcess(NewMixer _mixer, double massinprocess)
        {
            BatchQueue.Enqueue(_mixer);
            _massInManufacturePendingToReceive += massinprocess;
        }
        public Amount MassInTransit => new Amount(_massInManufacturePendingToReceive, MassUnits.KiloGram);

        // 2. Lo que ya llegó (Received)
        public Amount MassReceived => new Amount(_massReceivedFromManufacture, MassUnits.KiloGram);

        // 3. El total de la orden (Required)
        public Amount MassRequired => new Amount(_massRequiredFromManufacture, MassUnits.KiloGram);

        // 4. EL REAL REMANENTE (La Resta)
        // Exponemos el resultado del cálculo protected
        public Amount MassRemainingToOrder => new Amount(_massRemainingFromManufacture, MassUnits.KiloGram);

        // Cálculo protegido contra negativos visuales
        protected double _massRemainingFromManufacture
        {
            get
            {
                double remaining = _massRequiredFromManufacture - _massReceivedFromManufacture - _massInManufacturePendingToReceive;
                return remaining < MASS_TOLERANCE ? 0 : remaining;
            }
        }

        // Visión Global (Físico + Lo que viene en camino)
        protected double _totalMassInProcess => _currentLevel + _massInManufacturePendingToReceive;
        public Amount MassInProcess => new Amount(_totalMassInProcess, MassUnits.KiloGram);

        // KPI: Tiempo para quedarse seco considerando lo que viene en camino
        protected double _pendingTimeEmptyMassInProcess =>
            (_massRequiredFromManufacture == 0 || _AverageOutletFlow == 0)
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
            // A. Física (Nivel)
            base.SetInletFlow(inletflow);

            // B. Contabilidad (Balance de Masa)
            _massReceivedFromManufacture += inletflow;
            _massInManufacturePendingToReceive -= inletflow;

            // C. Limpieza de residuos matemáticos (Tolerance)

        }
        public void ReceiveStopDischargFromMixer()
        {
            AssignedMixer = null!;
            if (BatchQueue.TryDequeue(out var _mixer))
            {
                AssignedMixer = _mixer;
                TransitionInletState(new NewInletRecipedTankAvailableState(this));
            }
        }

        // --- 4. REPORTES ESPECÍFICOS (La parte que faltaba) ---
        protected override void AddSpecificReportData(List<NewInstantaneousReport> reportList)
        {
            // IMPORTANTE: Llamamos a la base primero para que pinte Capacidad, Nivel y TimeToEmpty físico
            base.AddSpecificReportData(reportList);
            // 2. PRODUCTO ACTUAL (Lo que pediste)
            if (CurrentMaterial != null)
            {
                reportList.Add(new NewInstantaneousReport("Product", CurrentMaterial.Name, IsBold: true, Color: "#673AB7"));
            }
            // Ahora agregamos la capa "Logística"
            // Solo mostramos esto si hay una orden activa (Required > 0) para no ensuciar la UI
            if (_massRequiredFromManufacture > MASS_TOLERANCE)
            {
                reportList.Add(new NewInstantaneousReport("--- ORDER INFO ---", "", Color: "#000000", IsBold: true));

                reportList.Add(new NewInstantaneousReport("Required",
                    MassRequired.ToString(), // Usamos nombre corto
                    Color: "#9C27B0"));

                reportList.Add(new NewInstantaneousReport("Received",
                    MassReceived.ToString(),
                    Color: "#4CAF50"));

                // Aquí usamos la propiedad correcta para "In Transit"
                reportList.Add(new NewInstantaneousReport("In Transit",
                    MassInTransit.ToString(),
                    Color: "#FFC107"));

                //Opcional: Mostrar cuánto falta (Remaining real)


                reportList.Add(new NewInstantaneousReport("Balance",
                    MassRemainingToOrder.ToString(),
                    Color: "#F44336"));


                // KPI Financiero
                if (_pendingTimeEmptyMassInProcess > 0)
                {
                    reportList.Add(new NewInstantaneousReport("Strategic TTL",
                        PendingTimeEmptyMassInProcess.ToString(),
                        IsBold: true, Color: "#673AB7"));
                }
            }
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
            // 1. CHEQUEO DE LLENADO
            // Usamos HiLevelControl como el límite de seguridad operativa
            if (_tank.CurrentLevel.GetValue(MassUnits.KiloGram) >= _tank.HiLevelControl.GetValue(MassUnits.KiloGram))
            {
                // CASO A: El dueño es un MIXER (Proceso Batch)
                // No queremos "matar" al Mixer, solo pausarlo.
                if (_tank.CurrentOwner is NewMixer mixer)
                {
                    // --- CORRECCIÓN CRÍTICA: NUEVA ARQUITECTURA ---
                    // Usamos MasterWaiting.
                    // El Mixer pasa a estado Naranja: "Waiting for {TankName}"
                    mixer.TransitionGlobalState(
                        new GlobalState_MasterWaiting(mixer, _tank)
                    );

                    // El tanque pasa al estado de "Retención con Histéresis" que arreglamos antes.
                    // Ese estado se encargará de despertar al Mixer cuando baje el nivel (al 85% por ejemplo).
                    _tank.TransitionInletState(new NewRecipedTankForMixerFullState(_tank));
                    return;
                }

                // CASO B: El dueño es un SKID (Proceso Continuo)
                // En skids, llenarse suele significar "Fin del Lote".
                if (_tank.CurrentOwner is NewSkid skid)
                {
                    // Ordenamos parada total y liberación de recursos
                    skid.ReceiveStopCommand();

                    // El tanque queda libre para quien quiera usarlo después
                    _tank.TransitionInletState(new NewInletRecipedTankAvailableState(_tank));
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
    //public class NewRecipedTankForMixerFullState : INewInletState
    //{
    //    private readonly NewRecipedInletTank _tank;

    //    public NewRecipedTankForMixerFullState(NewRecipedInletTank tank)
    //    {
    //        _tank = tank;
    //    }

    //    public bool IsProductive => true;
    //    public string StateLabel => "Receiving Product Starved";
    //    public string HexColor => "Gemini me dara el color aqui";

    //    public void Calculate() { }

    //    public void CheckTransitions()
    //    {
    //        if (_tank.CurrentOwner is NewMixer mixer)
    //        {
    //            // MATEMÁTICA CORRECTA:
    //            // 1. ¿Cuánto espacio libre tengo? (Capacidad - Nivel Actual)
    //            double emptySpace = _tank.Capacity.GetValue(MassUnits.KiloGram) - _tank.CurrentLevel.GetValue(MassUnits.KiloGram);

    //            // 2. ¿Cuánto tiene el mixer todavía?
    //            double mixerMass = mixer.CurrentLevel.GetValue(MassUnits.KiloGram);

    //            // 3. LA CONDICIÓN: ¿Cabe todo lo del mixer en mi hueco?
    //            // (Opcional: Podrías usar una histéresis simple como 'emptySpace > 100', 
    //            // pero respetando tu lógica de "si cabe todo"):
    //            if (emptySpace >= mixerMass)
    //            {
    //                // ¡SÍ CABE!

    //                // 1. Despertamos al Mixer (Quitamos el Starved Global)
    //                // Verificamos que esté pausado por nosotros para no romper nada
    //                if (mixer.GlobalState is NewGlobalStarvedByResourceState)
    //                {
    //                    mixer.TransitionGlobalState(new NewGlobalAvailableState(mixer));
    //                }

    //                // 2. Nosotros volvemos a recibir
    //                _tank.TransitionInletState(new NewRecipedTankReceivingState(_tank));
    //            }

    //            // NOTA: Si el mixer tiene MUCHO producto (ej. 2000kg) y el tanque es pequeño (1000kg),
    //            // esta condición (emptySpace >= mixerMass) NUNCA se cumplirá.
    //            // Si ese caso es posible, deberías cambiar la condición a:
    //            // if (emptySpace > _tank.Capacity * 0.10) // Si tengo al menos 10% de espacio, dale.
    //        }
    //    }
    //}
}

