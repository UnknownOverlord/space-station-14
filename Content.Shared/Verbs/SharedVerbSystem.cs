using System;
using System.Collections.Generic;
using Content.Shared.ActionBlocker;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Utility;

namespace Content.Shared.Verbs
{
    public abstract class SharedVerbSystem : EntitySystem
    {
        [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
        [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
        [Dependency] protected readonly SharedContainerSystem ContainerSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeAllEvent<ExecuteVerbEvent>(HandleExecuteVerb);
        }

        private void HandleExecuteVerb(ExecuteVerbEvent args, EntitySessionEventArgs eventArgs)
        {
            var user = eventArgs.SenderSession.AttachedEntity;
            if (user == null)
                return;

            // Get the list of verbs. This effectively also checks that the requested verb is in fact a valid verb that
            // the user can perform.
            var verbs = GetLocalVerbs(args.Target, user.Value, args.RequestedVerb.GetType());

            // Note that GetLocalVerbs might waste time checking & preparing unrelated verbs even though we know
            // precisely which one we want to run. However, MOST entities will only have 1 or 2 verbs of a given type.
            // The one exception here is the "other" verb type, which has 3-4 verbs + all the debug verbs.

            // Find the requested verb.
            if (verbs.TryGetValue(args.RequestedVerb, out var verb))
                ExecuteVerb(verb, user.Value, args.Target);
        }

        /// <summary>
        ///     Raises a number of events in order to get all verbs of the given type(s) defined in local systems. This
        ///     does not request verbs from the server.
        /// </summary>
        public SortedSet<Verb> GetLocalVerbs(EntityUid target, EntityUid user, Type type, bool force = false, bool all = false)
        {
            return GetLocalVerbs(target, user, new List<Type>() { type }, force, all);
        }

        /// <summary>
        ///     Raises a number of events in order to get all verbs of the given type(s) defined in local systems. This
        ///     does not request verbs from the server.
        /// </summary>
        public SortedSet<Verb> GetLocalVerbs(EntityUid target, EntityUid user, List<Type> types, bool force = false, bool all = false)
        {
            SortedSet<Verb> verbs = new();

            // accessibility checks
            bool canAccess = false;
            if (force || target == user)
                canAccess = true;
            else if (EntityManager.EntityExists(target) && _interactionSystem.InRangeUnobstructed(user, target))
            {
                if (ContainerSystem.IsInSameOrParentContainer(user, target))
                    canAccess = true;
                else
                    // the item might be in a backpack that the user has open
                    canAccess = _interactionSystem.CanAccessViaStorage(user, target);
            }

            // A large number of verbs need to check action blockers. Instead of repeatedly having each system individually
            // call ActionBlocker checks, just cache it for the verb request.
            var canInteract = force || _actionBlockerSystem.CanInteract(user, target);

            EntityUid? @using = null;
            if (TryComp(user, out SharedHandsComponent? hands) && (force || _actionBlockerSystem.CanUseHeldEntity(user)))
            {
                hands.TryGetActiveHeldEntity(out @using);

                // Check whether the "Held" entity is a virtual pull entity. If yes, set that as the entity being "Used".
                // This allows you to do things like buckle a dragged person onto a surgery table, without click-dragging
                // their sprite.

                if (TryComp(@using, out HandVirtualItemComponent? pull))
                {
                    @using = pull.BlockingEntity;
                }
            }

            if (types.Contains(typeof(InteractionVerb)))
            {
                var verbEvent = new GetVerbsEvent<InteractionVerb>(user, target, @using, hands, canInteract, canAccess);
                RaiseLocalEvent(target, verbEvent);
                verbs.UnionWith(verbEvent.Verbs);
            }

            if (types.Contains(typeof(AlternativeVerb)))
            {
                var verbEvent = new GetVerbsEvent<AlternativeVerb>(user, target, @using, hands, canInteract, canAccess);
                RaiseLocalEvent(target, verbEvent);
                verbs.UnionWith(verbEvent.Verbs);
            }

            if (types.Contains(typeof(ActivationVerb)))
            {
                var verbEvent = new GetVerbsEvent<ActivationVerb>(user, target, @using, hands, canInteract, canAccess);
                RaiseLocalEvent(target, verbEvent);
                verbs.UnionWith(verbEvent.Verbs);
            }

            if (types.Contains(typeof(ExamineVerb)))
            {
                var verbEvent = new GetVerbsEvent<ExamineVerb>(user, target, @using, hands, canInteract, canAccess);
                RaiseLocalEvent(target, verbEvent);
                verbs.UnionWith(verbEvent.Verbs);
            }

            // generic verbs
            if (types.Contains(typeof(Verb)))
            {
                var verbEvent = new GetVerbsEvent<Verb>(user, target, @using, hands, canInteract, canAccess);
                RaiseLocalEvent(target, verbEvent);
                verbs.UnionWith(verbEvent.Verbs);
            }

            return verbs;
        }

        /// <summary>
        ///     Execute the provided verb.
        /// </summary>
        /// <remarks>
        ///     This will try to call the action delegates and raise the local events for the given verb.
        /// </remarks>
        public abstract void ExecuteVerb(Verb verb, EntityUid user, EntityUid target, bool forced = false);
    }
}
