﻿using System;
using System.Collections.Generic;
using System.Linq;
using TwistedLogik.Ultraviolet.UI.Presentation.Animations;
using TwistedLogik.Ultraviolet.UI.Presentation.Uvss;
using TwistedLogik.Ultraviolet.UI.Presentation.Uvss.Diagnostics;
using TwistedLogik.Ultraviolet.UI.Presentation.Uvss.Syntax;

namespace TwistedLogik.Ultraviolet.UI.Presentation.Styles
{
    /// <summary>
    /// Contains methods for compiling UVSS syntax trees into UVSS documents.
    /// </summary>
    internal static class UvssCompiler
    {
        /// <summary>
        /// Compiles an Ultraviolet Style Sheet (UVSS) document from the specified abstract syntax tree.
        /// </summary>
        /// <param name="tree">A <see cref="UvssDocumentSyntax"/> that represents the
        /// abstract syntax tree to compile.</param>
        /// <returns>A new instance of <see cref="UvssDocument"/> that represents the compiled data.</returns>
        public static UvssDocument Compile(UvssDocumentSyntax tree)
        {
            // Fail to compile if the tree reports any error diagnostics.
            var errors = tree.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error);
            if (errors.Any())
            {
                var errorsList = String.Join(Environment.NewLine, errors.Select(x => x.Message));
                var errorsMessage = PresentationStrings.StyleSheetParserError.Format(errorsList);
                throw new UvssException(errorsMessage);
            }

            // Compile a list of rule sets and storyboards.
            var docRuleSets = new List<UvssRuleSet>();
            var docStoryboards = new List<UvssStoryboard>();

            for (int i = 0; i < tree.Content.Count; i++)
            {
                var contentNode = tree.Content[i];
                switch (contentNode.Kind)
                {
                    case SyntaxKind.RuleSet:
                        var ruleSet = CompileRuleSet((UvssRuleSetSyntax)contentNode);
                        docRuleSets.Add(ruleSet);
                        break;

                    case SyntaxKind.Storyboard:
                        var storyboard = CompileStoryboard((UvssStoryboardSyntax)contentNode);
                        docStoryboards.Add(storyboard);
                        break;

                    default:
                        throw new UvssException("TODO");
                }
            }

            return new UvssDocument(docRuleSets, docStoryboards);
        }

        /// <summary>
        /// Gets the full property name represented by the specified syntax node.
        /// </summary>
        private static String GetPropertyName(UvssPropertyNameSyntax node)
        {
            if (node.AttachedPropertyOwnerNameIdentifier == null)
                return node.PropertyNameIdentifier.Text;

            return String.Format("{0}.{1}",
                node.AttachedPropertyOwnerNameIdentifier.Text,
                node.PropertyNameIdentifier.Text);
        }

        /// <summary>
        /// Gets the full event name represented by the specified syntax node.
        /// </summary>
        private static String GetEventName(UvssEventNameSyntax node)
        {
            if (node.AttachedEventOwnerNameIdentifier == null)
                return node.EventNameIdentifier.Text;

            return String.Format("{0}.{1}",
                node.AttachedEventOwnerNameIdentifier.Text,
                node.EventNameIdentifier.Text);
        }

        /// <summary>
        /// Parses the value of the specified optional identifier into a <see cref="LoopBehavior"/>
        /// value, if the identifier exists.
        /// </summary>
        private static LoopBehavior ParseOptionalLoopBehavior(UvssIdentifierBaseSyntax identifier)
        {
            if (identifier == null)
                return LoopBehavior.None;

            var text = identifier.Text;
            switch (text)
            {
                case KnownLoopBehaviors.None:
                    return LoopBehavior.None;

                case KnownLoopBehaviors.Loop:
                    return LoopBehavior.Loop;

                case KnownLoopBehaviors.Reverse:
                    return LoopBehavior.Reverse;

                default:
                    throw new UvssException("TODO");
            }
        }

        /// <summary>
        /// Compiles a <see cref="UvssNavigationExpression"/> from the specified syntax node.
        /// </summary>
        private static UvssNavigationExpression CompileNavigationExpression(UvssNavigationExpressionSyntax node)
        {
            var navigationProperty =
                GetPropertyName(node.PropertyName);

            var navigationPropertyType =
                node.TypeNameIdentifier.Text;

            // TODO: indices
            return new UvssNavigationExpression(navigationProperty, navigationPropertyType);
        }

        /// <summary>
        /// Compiles a <see cref="UvssSelector"/> from the specified syntax node.
        /// </summary>
        private static UvssSelector CompileSelector(UvssSelectorBaseSyntax selector)
        {
            var parts = new List<UvssSelectorPart>();
            var qualifier = UvssSelectorPartQualifier.None;

            for (int i = 0; i < selector.Components.Count; i++)
            {
                var component = selector.Components[i];
                switch (component.Kind)
                {
                    case SyntaxKind.SelectorPart:
                        {
                            var part = CompileSelectorPart((UvssSelectorPartSyntax)component, qualifier);
                            parts.Add(part);
                            qualifier = UvssSelectorPartQualifier.None;
                        }
                        break;
                        
                    case SyntaxKind.GreaterThanToken:
                        qualifier = UvssSelectorPartQualifier.VisualChild;
                        break;

                    case SyntaxKind.GreaterThanGreaterThanToken:
                        qualifier = UvssSelectorPartQualifier.TemplatedChild;
                        break;

                    case SyntaxKind.GreaterThanQuestionMarkToken:
                        qualifier = UvssSelectorPartQualifier.LogicalChild;
                        break;
                }
            }
            return new UvssSelector(parts);
        }

        /// <summary>
        /// Compiles a <see cref="UvssSelectorPart"/> from the specified syntax node.
        /// </summary>
        private static UvssSelectorPart CompileSelectorPart(UvssSelectorPartSyntax node, UvssSelectorPartQualifier qualifier)
        {
            var element = 
                node.SelectedType?.SelectedTypeIdentifier?.Text;

            if (element == "*")
                element = null;

            var elementIsExact =
                node.SelectedType?.ExclamationMarkToken != null;

            var id =
                node.SelectedName?.SelectedNameIdentifier?.Text;

            var pseudoClass =
                node.PseudoClass?.ClassNameIdentifier?.Text;

            var classes = new List<String>();
            for (int i = 0; i < node.SelectedClasses.Count; i++)
            {
                var selectedClass = node.SelectedClasses[i];
                classes.Add(selectedClass.SelectedClassIdentifier.Text);
            }

            return new UvssSelectorPart(qualifier,
                element,
                elementIsExact,
                id,
                pseudoClass,
                classes);
        }

        /// <summary>
        /// Compiles a <see cref="UvssRuleSet"/> from the specified syntax node.
        /// </summary>
        private static UvssRuleSet CompileRuleSet(UvssRuleSetSyntax node)
        {
            var selectors = new List<UvssSelector>();
            for (int i = 0; i < node.Selectors.Count; i++)
            {
                var selectorNode = node.Selectors[i];
                var selector = CompileSelector(selectorNode);
                selectors.Add(selector);
            }
            
            var rules = new List<UvssRule>();
            var triggers = new List<UvssTrigger>();
            for (int i = 0; i < node.Body.Content.Count; i++)
            {
                var bodyNode = node.Body.Content[i];
                switch (bodyNode.Kind)
                {
                    case SyntaxKind.Rule:
                        {
                            var rule = CompileRule((UvssRuleSyntax)bodyNode);
                            rules.Add(rule);
                        }
                        break;

                    case SyntaxKind.Transition:
                        {
                            var transition = CompileTransition((UvssTransitionSyntax)bodyNode);
                            rules.Add(transition);
                        }
                        break;

                    case SyntaxKind.PropertyTrigger:
                        {
                            var trigger = CompilePropertyTrigger((UvssPropertyTriggerSyntax)bodyNode);
                            triggers.Add(trigger);
                        }
                        break;

                    case SyntaxKind.EventTrigger:
                        {
                            var trigger = CompileEventTrigger((UvssEventTriggerSyntax)bodyNode);
                            triggers.Add(trigger);
                        }
                        break;
                }
            }

            return new UvssRuleSet(
                new UvssSelectorCollection(selectors),
                new UvssRuleCollection(rules),
                new UvssTriggerCollection(triggers));
        }

        /// <summary>
        /// Compiles a <see cref="UvssRule"/> from the specified syntax node.
        /// </summary>
        private static UvssRule CompileRule(UvssRuleSyntax node)
        {
            var owner = node.PropertyName.AttachedPropertyOwnerNameIdentifier?.Text;
            var name = node.PropertyName.PropertyNameIdentifier.Text;
            var value = node.Value.Value;
            var isImportant = node.QualifierToken?.Kind == SyntaxKind.ImportantKeyword;

            return new UvssRule(
                new UvssRuleArgumentsCollection(null),
                owner,
                name,
                value,
                isImportant);
        }

        /// <summary>
        /// Compiles a <see cref="UvssRule"/> from the specified syntax node.
        /// </summary>
        private static UvssRule CompileTransition(UvssTransitionSyntax node)
        {
            var arguments = new List<String>(node.ArgumentList.ArgumentIdentifiers.Select(x => x.Text));
            var name = "transition";
            var value = node.Value.Value;
            var isImportant = node.QualifierToken?.Kind == SyntaxKind.ImportantKeyword;

            return new UvssRule(
                new UvssRuleArgumentsCollection(arguments),
                null,
                name,
                value,
                isImportant);
        }

        /// <summary>
        /// Compiles a <see cref="UvssPropertyTrigger"/> from the specified syntax node.
        /// </summary>
        private static UvssPropertyTrigger CompilePropertyTrigger(UvssPropertyTriggerSyntax node)
        {
            var isImportant = node.QualifierToken?.Kind == SyntaxKind.ImportantKeyword;
            var trigger = new UvssPropertyTrigger(isImportant);

            for (int i = 0; i < node.Conditions.Count; i++)
            {
                var conditionNode = node.Conditions[i];
                var condition = CompilePropertyTriggerCondition(conditionNode);
                trigger.Conditions.Add(condition);
            }

            foreach (var action in node.Actions)
                trigger.Actions.Add(CompileTriggerAction(action));

            return trigger;
        }

        /// <summary>
        /// Compiles a <see cref="UvssPropertyTriggerCondition"/> from the specified syntax node.
        /// </summary>
        private static UvssPropertyTriggerCondition CompilePropertyTriggerCondition(UvssPropertyTriggerConditionSyntax node)
        {
            var op = default(TriggerComparisonOp);
            var dpropName = GetPropertyName(node.PropertyName);
            var refval = node.PropertyValue.Value;

            switch (node.ComparisonOperatorToken.Kind)
            {
                case SyntaxKind.EqualsToken:
                    op = TriggerComparisonOp.Equals;
                    break;

                case SyntaxKind.NotEqualsToken:
                    op = TriggerComparisonOp.NotEquals;
                    break;

                case SyntaxKind.GreaterThanToken:
                    op = TriggerComparisonOp.GreaterThan;
                    break;

                case SyntaxKind.LessThanToken:
                    op = TriggerComparisonOp.LessThan;
                    break;

                case SyntaxKind.GreaterThanEqualsToken:
                    op = TriggerComparisonOp.GreaterThanOrEqualTo;
                    break;

                case SyntaxKind.LessThanEqualsToken:
                    op = TriggerComparisonOp.LessThanOrEqualTo;
                    break;

                default:
                    throw new UvssException("TODO");
            }

            return new UvssPropertyTriggerCondition(op, dpropName, refval);
        }

        /// <summary>
        /// Compiles a <see cref="UvssEventTrigger"/> from the specified syntax node.
        /// </summary>
        private static UvssEventTrigger CompileEventTrigger(UvssEventTriggerSyntax node)
        {
            var eventName = GetEventName(node.EventName);
            var arguments = node.ArgumentList == null ? Enumerable.Empty<String>() :
                node.ArgumentList.ArgumentTokens.Select(x => x.Text).ToList();
            var handled = arguments.Contains("handled");
            var setHandled = arguments.Contains("set-handled");
            var isImportant = node.QualifierToken?.Kind == SyntaxKind.ImportantKeyword;

            var trigger = new UvssEventTrigger(eventName, handled, setHandled, isImportant);

            foreach (var action in node.Actions)
                trigger.Actions.Add(CompileTriggerAction(action));

            return trigger;
        }

        /// <summary>
        /// Compiles a <see cref="TriggerAction"/> from the specified syntax node.
        /// </summary>
        private static TriggerAction CompileTriggerAction(UvssTriggerActionBaseSyntax node)
        {
            var action = default(TriggerAction);

            switch (node.Kind)
            {
                case SyntaxKind.PlayStoryboardTriggerAction:
                    action = CompilePlayStoryboardTriggerAction((UvssPlayStoryboardTriggerActionSyntax)node);
                    break;

                case SyntaxKind.PlaySfxTriggerAction:
                    action = CompilePlaySfxTriggerAction((UvssPlaySfxTriggerActionSyntax)node);
                    break;

                case SyntaxKind.SetTriggerAction:
                    action = CompileSetTriggerAction((UvssSetTriggerActionSyntax)node);
                    break;

                default:
                    throw new UvssException("TODO");
            }

            return action;
        }

        /// <summary>
        /// Compiles a <see cref="PlayStoryboardTriggerAction"/> from the specified syntax node.
        /// </summary>
        private static PlayStoryboardTriggerAction CompilePlayStoryboardTriggerAction(UvssPlayStoryboardTriggerActionSyntax node)
        {
            var storyboardName = node.Value.Value;
            var selector = node.Selector == null ? null :
                CompileSelector(node.Selector);

            return new PlayStoryboardTriggerAction(storyboardName, selector);
        }

        /// <summary>
        /// Compiles a <see cref="PlaySoundEffectTriggerAction"/> from the specified syntax node.
        /// </summary>
        private static PlaySoundEffectTriggerAction CompilePlaySfxTriggerAction(UvssPlaySfxTriggerActionSyntax node)
        {
            var sfxAssetID = 
                SourcedAssetID.Parse(node.Value.Value);

            return new PlaySoundEffectTriggerAction(sfxAssetID);
        }

        /// <summary>
        /// Compiles a <see cref="SetTriggerAction"/> from the specified syntax node.
        /// </summary>
        private static SetTriggerAction CompileSetTriggerAction(UvssSetTriggerActionSyntax node)
        {
            var dpropName = GetPropertyName(node.PropertyName);
            var selector = node.Selector == null ? null :
                CompileSelector(node.Selector);
            var value = node.Value.Value;

            return new SetTriggerAction(dpropName, selector, value);
        }

        /// <summary>
        /// Compiles a <see cref="UvssStoryboard"/> from the specified syntax node.
        /// </summary>
        private static UvssStoryboard CompileStoryboard(UvssStoryboardSyntax node)
        {
            var name =
                node.NameIdentifier.Text;

            var loopBehavior =
                ParseOptionalLoopBehavior(node.LoopIdentifier);

            var targets = new List<UvssStoryboardTarget>();
            for (int i = 0; i < node.Body.Content.Count; i++)
            {
                var targetNode = (UvssStoryboardTargetSyntax)node.Body.Content[i];
                var target = CompileStoryboardTarget(targetNode);
                targets.Add(target);
            }

            return new UvssStoryboard(
                name, 
                loopBehavior,
                new UvssStoryboardTargetCollection(targets));
        }

        /// <summary>
        /// Compiles a <see cref="UvssStoryboardTarget"/> from the specified syntax node.
        /// </summary>
        private static UvssStoryboardTarget CompileStoryboardTarget(UvssStoryboardTargetSyntax node)
        {
            var selector = node.Selector == null ? null :
                CompileSelector(node.Selector);

            var filter =
                CompileStoryboardTargetFilter(node.TypeNameIdentifier);

            var animations = new List<UvssStoryboardAnimation>();
            for (int i = 0; i < node.Body.Content.Count; i++)
            {
                var animationNode = (UvssAnimationSyntax)node.Body.Content[i];
                var animation = CompileStoryboardAnimation(animationNode);
                animations.Add(animation);
            }

            return new UvssStoryboardTarget(
                selector,
                filter,
                new UvssStoryboardAnimationCollection(animations));
        }

        /// <summary>
        /// Compiles a <see cref="UvssStoryboardTargetFilter"/> from the specified syntax node.
        /// </summary>
        private static UvssStoryboardTargetFilter CompileStoryboardTargetFilter(UvssIdentifierBaseSyntax identifier)
        {
            var filter = new UvssStoryboardTargetFilter();
            if (identifier == null)
            {
                filter.Add(nameof(FrameworkElement));
            }
            else
            {
                filter.Add(identifier.Text);
            }
            return filter;
        }

        /// <summary>
        /// Compiles a <see cref="UvssStoryboardAnimation"/> from the specified syntax node.
        /// </summary>
        private static UvssStoryboardAnimation CompileStoryboardAnimation(UvssAnimationSyntax node)
        {
            var animatedProperty =
                GetPropertyName(node.PropertyName);
            
            var navigationExpression = node.NavigationExpression == null ? null :
                CompileNavigationExpression(node.NavigationExpression);

            var keyframes = new List<UvssStoryboardKeyframe>();
            for (int i = 0; i < node.Body.Content.Count; i++)
            {
                var keyframeNode = (UvssAnimationKeyframeSyntax)node.Body.Content[i];
                var keyframe = CompileStoryboardKeyframe(keyframeNode);
                keyframes.Add(keyframe);
            }

            return new UvssStoryboardAnimation(
                animatedProperty,
                navigationExpression,
                new UvssStoryboardKeyframeCollection(keyframes));
        }

        /// <summary>
        /// Compiles a <see cref="UvssStoryboardKeyframe"/> from the specified syntax node.
        /// </summary>
        private static UvssStoryboardKeyframe CompileStoryboardKeyframe(UvssAnimationKeyframeSyntax node)
        {
            var easing =
                node.EasingIdentifier?.Text;

            var value =
                node.Value.Value;

            var time =
                Double.Parse(node.TimeToken.Text);

            return new UvssStoryboardKeyframe(easing, value, time);
        }
    }
}