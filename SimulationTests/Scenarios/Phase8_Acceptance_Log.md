# Phase 8 Acceptance Log — Dialogue across the village

Seed 42. Four Content NPCs with distinct moods, beliefs, memories
and known news. The template phrasing engine (P8-02) stands in for
the future AI adapter behind `IPhrasingEngine`; no AI services, no Unity.

## The setup (world truth vs NPC knowledge)

- **World truth:** the player really did steal 6 apples from Mira's stall.
- **Mira Holt** (apple-stall owner, happy) *saw* the theft.
- **Bram Stone** (guard, low) was *told* about it by Sella (hedges differently).
- **Sella Wren** (healer, neutral) was never there and knows nothing about the player.
- **Elswith Alder** (village elder, miserable) *inferred* the player stole
  apples from the general store — this never happened. Dialogue phrases beliefs, not truth.
- Three news items reach Millbrook from elsewhere: a festival at King's Rest,
  a wolf attack near Oakhollow, a good harvest at King's Rest.

## Mira Holt — apple-stall owner (mood: happy)

*Greeting:* "Ha. Good day."

*Asked about the player:* "I saw you take 6 apples from the apple stall."

*Shares news:* "Have you heard the news from King's Rest? The harvest was good in King's Rest."

## Bram Stone — guard (mood: low)

*Greeting:* "Oh, hello."

*Asked about the player:* "I heard you take 6 apples from the apple stall."

*Shares news:* "Have you heard the news from King's Rest? The harvest was good in King's Rest."

## Sella Wren — healer (mood: neutral)

*Greeting:* "Good day."

*Asked about the player:* "I do not know you well yet."

*Shares news:* "Have you heard the news from King's Rest? The harvest was good in King's Rest."

## Elswith Alder — village elder (mood: miserable)

*Greeting:* "Oh. It is you."

*Asked about the player:* "I figure you take 3 apples from the general store."

*Shares news:* "Word from King's Rest: the harvest was good in King's Rest."

## Knowledge vs truth, demonstrated

- Sella was never at the stall: asked about the player she says
  "I do not know you well yet." —
  no mention of apples, though the theft really happened.
- Elswith's theft never happened, yet asked about the player she says
  "I figure you take 3 apples from the general store." —
  dialogue phrases what she believes, not what is true.
- Mira saw it, Bram only heard it: their answers hedge differently
  ("saw" vs "heard"), though both name the same theft.

## Guarantees verified by the tests

- `VillageDialogueAcceptance`: every intent non-empty for every NPC;
  utterances differ with mood/belief; world digest byte-identical
  before and after all 24 utterances (6c39cdeb3c868444…).
- `DialogueIsDeterministicAcrossSessions`: same seed → byte-identical utterances.
- `DialogueSurvivesSaveLoad`: save → load → rebuild sheets → identical utterances.

