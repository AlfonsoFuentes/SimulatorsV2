using GeminiSimulator.NewFilesSimulations.ManufactureEquipments;
using QWENShared.DTOS.BackBoneSteps;
using QWENShared.DTOS.Materials;
using QWENShared.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using UnitSystem;

namespace GeminiSimulator.Materials
{
    /// <summary>
    /// Representa la ficha técnica de un material (MP, Producto o Intermedio).
    /// </summary>
    public class ProductDefinition
    {
        public override string ToString()
        {
            return $"{Name}";
        }
        public Guid Id { get; private set; }
        public string Code { get; private set; } // M_Number
        public string Name { get; private set; } // CommonName
        public MaterialType Type { get; private set; }

        public bool IsForWashing { get; private set; }

        // Receta (Opcional)
        public RecipeDefinition? Recipe { get; private set; }
        public ProductCategory Category { get; private set; }

        // CONSTRUCTOR LIMPIO
        public ProductDefinition(Guid id, string code, string name, MaterialType type, ProductCategory category, bool isForWashing)
        {
            Id = id;
            Code = code;
            Name = name;
            Type = type;
            Category = category;
            IsForWashing = isForWashing;
        }

        // Método para asignar la receta (Patrón Builder/Setter)
      

        // --- LÓGICA DE CLASIFICACIÓN ---

        public bool IsManufactured => Recipe != null && Recipe.Steps.Count > 0;

        public bool IsPurchased => !IsManufactured;

        public bool IsIntermediate => IsManufactured && Type == MaterialType.RawMaterialBackBone;

        public bool IsFinishedProduct => IsManufactured && Type == MaterialType.ProductBackBone;
        public List<RecipeStep> RecipeSteps { get; private set; } = new();

        // --- LA MATRIZ DE TIEMPOS CON UNIDADES ---
        // Obligamos a que el valor sea un Amount (ej: 3600 seg o 1 hora)
        public Dictionary<NewMixer, Amount> TheoreticalBatchTimesPerMixer { get; private set; } = new();

        // Registro usando la clase de unidades
        public void RegisterTheoreticalTime(NewMixer mixer, Amount totalTime)
        {
            TheoreticalBatchTimesPerMixer[mixer] = totalTime;
        }
        public void SetRecipe(List<RecipeStep> steps)
        {
            RecipeSteps = steps?.OrderBy(x => x.Order).ToList() ?? new List<RecipeStep>();
        }
    }
    public class RecipeDefinition
    {
        public List<RecipeStep> Steps { get; private set; }

        public RecipeDefinition(List<RecipeStep> steps)
        {
            // Asignamos la lista directamente.
            // Se asume que el Loader ya la ordenó, pero por seguridad podríamos ordenar aquí también.
            Steps = steps?.OrderBy(x => x.Order).ToList() ?? new List<RecipeStep>();
        }
    }
    public class RecipeStep
    {
        public int Order { get; private set; }
        public BackBoneStepType OperationType { get; private set; }

        // ID del ingrediente necesario. Null si es solo tiempo.
        public Guid? IngredientId { get; private set; }

        // Cantidad objetivo (% del tamaño del batch)
        public double TargetPercentage { get; private set; }

        public void SetDuration(Amount duration)
        {
            Duration = duration;
        }
        public Amount Duration { get; private set; }
        public Amount MassTarget { get; private set; } = null!;
        public void SetMassTarget(Amount massTarget)
        {
            MassTarget = massTarget;
        }

        public string IngredientName { get; private set; }

        // CONSTRUCTOR LIMPIO
        public RecipeStep(int order, BackBoneStepType type, Guid? ingredientId, double percentage, Amount duration,  string name)
        {
            Order = order;
            OperationType = type;
            IngredientId = ingredientId;
            TargetPercentage = percentage;
            Duration = duration;
            IngredientName = name;
          
        }

        public bool IsMaterialAddition => IngredientId != null && IngredientId != Guid.Empty && TargetPercentage > 0;
    }
}
