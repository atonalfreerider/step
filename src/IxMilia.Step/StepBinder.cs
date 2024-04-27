using System;
using System.Collections.Generic;
using System.Linq;
using IxMilia.Step.Schemas.ExplicitDraughting;
using IxMilia.Step.Syntax;

namespace IxMilia.Step
{
    class StepBinder(Dictionary<int, StepItem> itemMap)
    {
        readonly Dictionary<int, List<Tuple<StepSyntax, Action<StepBoundItem>>>> _unboundPointers = new Dictionary<int, List<Tuple<StepSyntax, Action<StepBoundItem>>>>();

        public void BindValue(StepSyntax syntax, Action<StepBoundItem> bindAction)
        {
            if (syntax is StepSimpleItemSyntax typedParameter)
            {
                StepItem item = StepItemBuilder.FromTypedParameter(this, typedParameter);
                StepBoundItem boundItem = new StepBoundItem(item, typedParameter);
                bindAction(boundItem);
            }
            else if (syntax is StepEntityInstanceReferenceSyntax itemInstance)
            {
                if (itemMap.TryGetValue(itemInstance.Id, out StepItem value))
                {
                    // pointer already defined, bind immediately
                    StepBoundItem boundItem = new StepBoundItem(value, itemInstance);
                    bindAction(boundItem);
                }
                else
                {
                    // not already defined, save it for later
                    if (!_unboundPointers.ContainsKey(itemInstance.Id))
                    {
                        _unboundPointers.Add(itemInstance.Id, new List<Tuple<StepSyntax, Action<StepBoundItem>>>());
                    }

                    _unboundPointers[itemInstance.Id].Add(Tuple.Create(syntax, bindAction));
                }
            }
            else if (syntax is StepAutoSyntax)
            {
                bindAction(StepBoundItem.AutoItem(syntax));
            }
            else
            {
                throw new StepReadException("Unable to bind pointer, this should be unreachable", syntax.Line, syntax.Column);
            }
        }

        public void BindRemainingValues()
        {
            foreach (int id in _unboundPointers.Keys)
            {
                if (!itemMap.TryGetValue(id, out StepItem item))
                {
                    StepSyntax syntax = _unboundPointers[id].First().Item1;
                    throw new StepReadException($"Cannot bind undefined pointer {id}", syntax.Line, syntax.Column);
                }

                foreach (Tuple<StepSyntax, Action<StepBoundItem>> binder in _unboundPointers[id])
                {
                    StepBoundItem boundItem = new StepBoundItem(item, binder.Item1);
                    binder.Item2(boundItem);
                }
            }
        }
    }
}
