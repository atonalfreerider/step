using IxMilia.Step.Schemas.ExplicitDraughting;
using IxMilia.Step.Syntax;

namespace IxMilia.Step
{
    class StepBoundItem(StepItem item, StepSyntax creatingSyntax)
    {
        public StepSyntax CreatingSyntax { get; } = creatingSyntax;
        public StepItem Item { get; } = item;
        public bool IsAuto { get; private set; }

        public TItemType AsType<TItemType>() where TItemType : StepItem
        {
            TItemType result = null;
            if (IsAuto)
            {
                // do nothing; null is expected
            }
            else
            {
                result = Item as TItemType;
                if (result == null)
                {
                    throw new StepReadException("Unexpected type", CreatingSyntax.Line, CreatingSyntax.Column);
                }
            }

            return result;
        }

        public static StepBoundItem AutoItem(StepSyntax creatingSyntax)
        {
            StepBoundItem boundItem = new StepBoundItem(null, creatingSyntax);
            boundItem.IsAuto = true;
            return boundItem;
        }
    }
}
