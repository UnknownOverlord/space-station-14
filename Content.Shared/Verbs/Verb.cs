using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using System;
using Content.Shared.Database;
using System.Collections.Generic;

namespace Content.Shared.Verbs
{
    /// <summary>
    ///     Verb objects describe actions that a user can take. The actions can be specified via an Action, local
    ///     events, or networked events. Verbs also provide text, icons, and categories for displaying in the
    ///     context-menu.
    /// </summary>
    [Serializable, NetSerializable, Virtual]
    public class Verb : IComparable
    {
        public static string DefaultTextStyleClass = "Verb";

        /// <summary>
        ///     Determines the priority of this type of verb when displaying in the verb-menu. See <see
        ///     cref="CompareTo"/>.
        /// </summary>
        public virtual int TypePriority => 0;

        /// <summary>
        ///     Style class for drawing in the context menu
        /// </summary>
        public string TextStyleClass = DefaultTextStyleClass;

        /// <summary>
        ///     This is an action that will be run when the verb is "acted" out.
        /// </summary>
        /// <remarks>
        ///     This delegate probably just points to some function in the system assembling this verb. This delegate
        ///     will be run regardless of whether <see cref="ExecutionEventArgs"/> is defined.
        /// </remarks>
        [NonSerialized]
        public Action? Act;

        /// <summary>
        ///     This is a general local event that will be raised when the verb is executed.
        /// </summary>
        /// <remarks>
        ///     If not null, this event will be raised regardless of whether <see cref="Act"/> was run. If this event
        ///     exists purely to call a specific system method, then <see cref="Act"/> should probably be used instead (method
        ///     events are a no-go).
        /// </remarks>
        [NonSerialized]
        public object? ExecutionEventArgs;

        /// <summary>
        ///     Where do direct the local event. If invalid, the event is not raised directed at any entity.
        /// </summary>
        [NonSerialized]
        public EntityUid EventTarget = EntityUid.Invalid;

        /// <summary>
        ///     If a verb is only defined client-side, this should be set to true.
        /// </summary>
        /// <remarks>
        ///     If true, the client will not also ask the server to run this verb when executed locally. This just
        ///     prevents unnecessary network events and "404-verb-not-found" log entries.
        /// </remarks>
        [NonSerialized]
        public bool ClientExclusive;

        /// <summary>
        ///     The text that the user sees on the verb button.
        /// </summary>
        public string Text = string.Empty;

        /// <summary>
        ///     Sprite of the icon that the user sees on the verb button.
        /// </summary>
        public SpriteSpecifier? Icon
        {
            get => _icon ??=
                IconTexture == null ? null : new SpriteSpecifier.Texture(new ResourcePath(IconTexture));
            set => _icon = value;
        }
        [NonSerialized]
        private SpriteSpecifier? _icon;

        /// <summary>
        ///     Name of the category this button is under. Used to group verbs in the context menu.
        /// </summary>
        public VerbCategory? Category;

        /// <summary>
        ///     Whether this verb is disabled.
        /// </summary>
        /// <remarks>
        ///     Disabled verbs are shown in the context menu with a slightly darker background color, and cannot be
        ///     executed. It is recommended that a <see cref="Message"/> message be provided outlining why this verb is
        ///     disabled.
        /// </remarks>
        public bool Disabled;

        /// <summary>
        ///     Optional informative message.
        /// </summary>
        /// <remarks>
        ///     This will be shown as a tooltip when hovering over this verb in the context menu. Additionally, iF a
        ///     <see cref="Disabled"/> verb is executed, this message will also be shown as a pop-up message. Useful for
        ///     disabled verbs to inform users about why they cannot perform a given action.
        /// </remarks>
        public string? Message;

        /// <summary>
        ///     Determines the priority of the verb. This affects both how the verb is displayed in the context menu
        ///     GUI, and which verb is actually executed when left/alt clicking.
        /// </summary>
        /// <remarks>
        ///     Bigger is higher priority (appears first, gets executed preferentially).
        /// </remarks>
        public int Priority;

        /// <summary>
        ///     Raw texture path used to load the <see cref="Icon"/> for displaying on the client.
        /// </summary>
        public string? IconTexture;

        /// <summary>
        ///     Whether or not to close the context menu after using it to run this verb.
        /// </summary>
        /// <remarks>
        ///     Setting this to false may be useful for repeatable actions, like rotating an object or maybe knocking on
        ///     a window.
        /// </remarks>
        public bool CloseMenu = true;

        /// <summary>
        ///     How important is this verb, for the purposes of admin logging?
        /// </summary>
        /// <remarks>
        ///     If this is just opening a UI or ejecting an id card, this should probably be low.
        /// </remarks>
        public LogImpact Impact = LogImpact.Low;

        /// <summary>
        ///     Whether this verb requires confirmation before being executed.
        /// </summary>
        public bool ConfirmationPopup = false;

        /// <summary>
        ///     Compares two verbs based on their <see cref="Priority"/>, <see cref="Category"/>, <see cref="Text"/>,
        ///     and <see cref="IconTexture"/>.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///     This is comparison is used when storing verbs in a SortedSet. The ordering of verbs determines both how
        ///     the verbs are displayed in the context menu, and the order in which alternative action verbs are
        ///     executed when alt-clicking.
        ///     </para>
        ///     <para>
        ///     If two verbs are equal according to this comparison, they cannot both be added to the same sorted set of
        ///     verbs. This is desirable, given that these verbs would also appear identical in the context menu.
        ///     Distinct verbs should always have a unique and descriptive combination of text, icon, and category.
        ///     </para>
        /// </remarks>
        public int CompareTo(object? obj)
        {
            if (obj is not Verb otherVerb)
                return -1;

            // Sort first by type-priority
            if (TypePriority != otherVerb.TypePriority)
                return otherVerb.TypePriority - TypePriority;

            // Then by verb-priority
            if (Priority != otherVerb.Priority)
                return otherVerb.Priority - Priority;

            // Then try use alphabetical verb categories. Uncategorized verbs always appear first.
            if (Category?.Text != otherVerb.Category?.Text)
            {
                return string.Compare(Category?.Text, otherVerb.Category?.Text, StringComparison.CurrentCulture);
            }

            // Then try use alphabetical verb text.
            if (Text != otherVerb.Text)
            {
                return string.Compare(Text, otherVerb.Text, StringComparison.CurrentCulture);
            }

            // Finally, compare icon texture paths. Note that this matters for verbs that don't have any text (e.g., the rotate-verbs)
            return string.Compare(IconTexture, otherVerb.IconTexture, StringComparison.CurrentCulture);
        }

        /// <summary>
        ///     Collection of all verb types, along with string keys.
        /// </summary>
        /// <remarks>
        ///     Useful when iterating over verb types, though maybe this should be obtained and stored via reflection or
        ///     something (list of all classes that inherit from Verb). Currently used for networking (apparently Type
        ///     is not serializable?), and resolving console commands.
        /// </remarks>
        public static List<Type> VerbTypes = new()
        {
            { typeof(Verb) },
            { typeof(InteractionVerb) },
            { typeof(AlternativeVerb) },
            { typeof(ActivationVerb) },
            { typeof(ExamineVerb) }
        };
    }

    /// <summary>
    ///    Primary interaction verbs. This includes both use-in-hand and interacting with external entities.
    /// </summary>
    /// <remarks>
    ///    These verbs those that involve using the hands or the currently held item on some entity. These verbs usually
    ///    correspond to interactions that can be triggered by left-clicking or using 'Z', and often depend on the
    ///    currently held item. These verbs are collectively shown first in the context menu.
    /// </remarks>
    [Serializable, NetSerializable]
    public sealed class InteractionVerb : Verb
    {
        public new static string DefaultTextStyleClass = "InteractionVerb";
        public override int TypePriority => 4;

        public InteractionVerb() : base()
        {
            TextStyleClass = DefaultTextStyleClass;
        }
    }

    /// <summary>
    ///     Verbs for alternative-interactions.
    /// </summary>
    /// <remarks>
    ///     When interacting with an entity via alt + left-click/E/Z the highest priority alt-interact verb is executed.
    ///     These verbs are collectively shown second-to-last in the context menu.
    /// </remarks>
    [Serializable, NetSerializable]
    public sealed class AlternativeVerb : Verb
    {
        public override int TypePriority => 2;
        public new static string DefaultTextStyleClass = "AlternativeVerb";

        public AlternativeVerb() : base()
        {
            TextStyleClass = DefaultTextStyleClass;
        }
    }

    /// <summary>
    ///    Activation-type verbs.
    /// </summary>
    /// <remarks>
    ///    These are verbs that activate an item in the world but are independent of the currently held items. For
    ///    example, opening a door or a GUI. These verbs should correspond to interactions that can be triggered by
    ///    using 'E', though many of those can also be triggered by left-mouse or 'Z' if there is no other interaction.
    ///    These verbs are collectively shown second in the context menu.
    /// </remarks>
    [Serializable, NetSerializable]
    public sealed class ActivationVerb : Verb
    {
        public override int TypePriority => 1;
        public new static string DefaultTextStyleClass = "ActivationVerb";

        public ActivationVerb() : base()
        {
            TextStyleClass = DefaultTextStyleClass;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ExamineVerb : Verb
    {
        public override int TypePriority => 0;

        public bool ShowOnExamineTooltip = true;
    }
}
