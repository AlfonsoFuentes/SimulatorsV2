using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.Context;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.PlantUnits.Lines;
using QWENShared.Enums;

namespace GeminiSimulator.NewFilesSimulations.ManufactureEquipments
{
    
    public abstract class NewManufacture : NewPlantUnit
    {
     

        public List<NewPump> InletPumps { get; private set; } = new List<NewPump>();
        public NewOperator? AssignedOperator { get; private set; }

        public NewManufacture(Guid id, string name, ProcessEquipmentType type, FocusFactory focusFactory)
            : base(id, name, type, focusFactory)
        {
           
        }
       
   
        public abstract void ReceiveStartCommand(NewRecipedInletTank wipTank);
        protected abstract void ExecuteStartLogic(NewRecipedInletTank wipTank);

        // --- 3. EL GESTOR DE SALIDA (Revisar la Cola) ---
        // Este método debe ser llamado por el hijo al final de 'ReceiveStopCommand'
       

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
        public string SubStateName => _manufacture.SubStatusMessage;
        NewMixer _manufacture;
        public NewManufactureBatchingState(NewMixer manufacture)
        {
            _manufacture = manufacture;
        }
        public bool IsProductive => true;    //Indica que el equipo esta disponible para producir pero nadie lo usa

        public string StateLabel => _manufacture.StatusMessage;


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
