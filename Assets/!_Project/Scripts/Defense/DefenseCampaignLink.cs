using UnityEngine;

namespace CodingGame.Defense
{
    // Resources/DefenseCampaign: the campaign asset the Defense scene's battle uses, for the developer windows outside the
    // battle (DefenseDevWindow's F2 balance window on the title or the stage map). The asset itself stays where the tools find it.
    public sealed class DefenseCampaignLink : ScriptableObject
    {
        public DefenseCampaignDefinition campaign;
    }
}