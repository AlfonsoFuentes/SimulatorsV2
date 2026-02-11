using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.Tanks;
using QWENShared.Enums;

namespace GeminiSimulator.NewFilesSimulations.ManufactureEquipments
{
    public abstract class NewManufacture : NewPlantUnit
    {
        // Cola de espera para órdenes que llegan cuando estamos ocupados
        protected Queue<NewRecipedInletTank> _wipTankQueue = new Queue<NewRecipedInletTank>();

        // Inicializamos la lista vacía para evitar errores si alguien la llama antes de tiempo
        public List<NewPump> InletPumps { get; private set; } = new List<NewPump>();
        public NewOperator? AssignedOperator { get; private set; }

        public NewManufacture(Guid id, string name, ProcessEquipmentType type, FocusFactory focusFactory)
            : base(id, name, type, focusFactory)
        {
            // NO inicializamos InletPumps aquí porque 'Inputs' aún está vacío en el constructor.
        }

        // --- 1. EL GATEKEEPER (Gestor de Entrada) ---
        // Este método recibe la orden y decide si se ejecuta YA o se va a la cola.
        // NO es virtual, para obligar a los hijos a respetar esta lógica.
        public virtual void ReceiveStartCommand(NewRecipedInletTank wipTank)
        {
            // Lógica base: Si estoy libre, arranco. Si no, a la cola.
            if (OutletState is not NewManufactureAvailableState)
            {
                _wipTankQueue.Enqueue(wipTank);
                return;
            }
            CurrentMaterial = wipTank.CurrentMaterial;
            ExecuteStartLogic(wipTank);
        }

        // --- 2. EL GANCHO ABSTRACTO (El Músculo) ---
        // Esto es lo que el Skid (o Mixer) debe implementar.
        // Aquí va la lógica de "TryReserveResources", "TransitionState", etc.
        protected abstract void ExecuteStartLogic(NewRecipedInletTank wipTank);

        // --- 3. EL GESTOR DE SALIDA (Revisar la Cola) ---
        // Este método debe ser llamado por el hijo al final de 'ReceiveStopCommand'
        protected void CheckQueueAndStartNext()
        {
            if (_wipTankQueue.Count > 0)
            {
                // Sacamos al siguiente de la fila
                var nextWip = _wipTankQueue.Dequeue();

                // Seteamos el material del siguiente batch
                CurrentMaterial = nextWip.CurrentMaterial;

                // Ejecutamos inmediatamente
                ExecuteStartLogic(nextWip);
            }
            else
            {
                // Si no hay nadie, a descansar (Available)
                TransitionOutletState(new NewManufactureAvailableState(this));
            }
        }

        // Método abstracto de parada que el hijo debe implementar
        public abstract void ReceiveStopCommand();

        // --- 4. CICLO DE VIDA (Inicialización tardía) ---
        public override void CheckInitialStatus(DateTime initialDate)
        {
            base.CheckInitialStatus(initialDate);

            // AQUÍ es el momento correcto para llenar las listas, 
            // porque el simulador ya ejecutó 'WireUp' y conectó los cables.
            InletPumps = Inputs.OfType<NewPump>().ToList();
            AssignedOperator = Inputs.OfType<NewOperator>().FirstOrDefault();

            // Arrancamos disponibles
            TransitionOutletState(new NewManufactureAvailableState(this));
        }
    }
    public class NewManufactureAvailableState : INewOutletState
    {
        public string SubStateName => string.Empty;
        NewManufacture _manufacture;
        public NewManufactureAvailableState(NewManufacture manufacture)
        {
            _manufacture = manufacture;
        }
        public bool IsProductive => true;    //Indica que el equipo esta disponible para producir pero nadie lo usa

        public string StateLabel => _manufacture is NewMixer ? "Mixer available" : "Skid available";

        public string HexColor => "Gemini me ayuda a definir el color";

        public void Calculate()
        {

        }

        public void CheckTransitions()
        {

        }
    }
    public class NewManufactureBatchingState : INewOutletState
    {
        public string SubStateName => _manufacture.CurrentStep?.GetSubStatusMessage() ?? string.Empty;
        NewMixer _manufacture;
        public NewManufactureBatchingState(NewMixer manufacture)
        {
            _manufacture = manufacture;
        }
        public bool IsProductive => true;    //Indica que el equipo esta disponible para producir pero nadie lo usa

        public string StateLabel => _manufacture.CurrentStep?.GetStatusMessage() ?? string.Empty;


        public string HexColor => "Gemini me ayuda a definir el color";

        public void Calculate()
        {
            _manufacture.BatchManager.Calculate();

        }

        public void CheckTransitions()
        {

        }
    }

}
