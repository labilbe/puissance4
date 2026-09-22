# Puissance 4

Jeu de **Puissance 4** (grille 7×6) en C# / .NET 10 : un moteur de règles complet, une IA
alpha-bêta à quatre niveaux, une interface graphique Blazor WebAssembly et une interface console.

```
  1   2   3   4   5   6   7
+---+---+---+---+---+---+---+
| . | . | . | . | . | . | . |
+---+---+---+---+---+---+---+
| . | . | . | . | . | . | . |
+---+---+---+---+---+---+---+
| . | . | . | R | . | . | . |
+---+---+---+---+---+---+---+
| . | . | . | J | R | . | . |
+---+---+---+---+---+---+---+
| . | . | R | J | J | . | . |
+---+---+---+---+---+---+---+
| . | J | R | R | J | R | . |
+---+---+---+---+---+---+---+
```

## Jouer dans le navigateur

```bash
dotnet run --project src/Puissance4.Web
```

Tout tourne côté client : le moteur et l'IA sont compilés en WebAssembly, il n'y a pas de serveur
de jeu. On clique n'importe où dans une colonne — un jeton fantôme montre d'avance la case où il
se posera —, et le jeton tombe. Quand l'adversaire menace d'aligner au coup suivant, la partie le
dit ; quand quelqu'un gagne, les quatre jetons de l'alignement s'éclairent.

Le panneau « Partie au format PCN » copie la partie en cours, l'enregistre en `.pcn`, ou en ouvre
une autre que l'on colle.

## Jouer dans le terminal

```bash
dotnet run --project src/Puissance4.Cli
```

Sans argument, le programme demande le mode de jeu et le niveau. En ligne de commande :

```bash
dotnet run --project src/Puissance4.Cli -- --humain-vs-ia --niveau difficile
dotnet run --project src/Puissance4.Cli -- --ia-vs-ia --niveau expert --sans-numeros
```

| Option | Effet |
| --- | --- |
| `--humain-vs-ia` | Vous jouez les Rouges, qui commencent (défaut) |
| `--ia-vs-humain` | Vous jouez les Jaunes |
| `--humain-vs-humain` | Deux joueurs sur le même clavier |
| `--ia-vs-ia` | Démonstration, l'ordinateur joue les deux camps |
| `--niveau <n>` | `facile`, `moyen`, `difficile`, `expert` |
| `--ouvrir <fichier>` | Reprend une partie enregistrée au format PCN |
| `--sans-numeros` | Masque les numéros de colonnes au-dessus de la grille |

Pendant la partie, un coup tient dans un chiffre : le **numéro de la colonne**, de 1 à 7 — la
lettre de la colonne, de `a` à `g`, est acceptée aussi. Les commandes `coups`, `annuler`,
`enregistrer`, `ouvrir` et `quitter` sont disponibles à tout moment.

## Échanger des parties : le PCN

Le Puissance 4 n'a pas de format normalisé comparable au PGN des échecs ou au PDN des dames. On en
reprend donc la forme — des en-têtes entre crochets, puis les coups numérotés par tour — sous le
nom de PCN, avec pour seule notation le numéro de colonne :

```
[Event "Tournoi du mercredi"]
[Site "?"]
[Date "2026.09.22"]
[Round "-"]
[Red "Dupont"]
[Yellow "Martin"]
[Result "1-0"]
[GameType "Connect4-7x6"]

1. 4 4 2. 5 3 3. 3 6 4. 6 1-0
```

En console :

```bash
dotnet run --project src/Puissance4.Cli -- --ouvrir partie.pcn
```

et, en cours de partie, les commandes `enregistrer [fichier]` et `ouvrir <fichier>`.

Ce qui est pris en charge : les en-têtes du sept-tag roster, `GameType "Connect4-7x6"` (les autres
valeurs sont refusées avec un message explicite), les positions de départ non standard via `SetUp`
et `FEN`, plusieurs parties dans un même fichier, les commentaires `{…}` et `;…`, les variantes
`(…)` et les annotations `$n` — ignorés à la lecture.

À la lecture, on accepte aussi la **suite de colonnes nue** — `4453` — qui est la façon dont les
solveurs et les bases de positions publient les parties de Puissance 4. C'est par elle qu'on fait
entrer une position venue d'ailleurs ; coller `4453` dans le panneau PCN suffit.

Le champ FEN est calqué sur celui des échecs : les six rangées, du haut vers le bas, séparées par
des barres obliques, un chiffre comptant les cases vides consécutives, puis le trait. La grille de
l'exemple ci-dessus après quatre coups s'écrit `7/7/7/7/3J3/2JRR2 r`. La lecture refuse un jeton
flottant : une grille de Puissance 4 n'a pas de trou.

## Règles implémentées

- grille de **7 colonnes sur 6 rangées**, 21 jetons par camp, les Rouges commencent ;
- on choisit une colonne, le jeton **tombe sur la première case libre** ;
- gagne le premier camp qui **aligne quatre jetons**, horizontalement, verticalement ou en
  diagonale ;
- la partie est nulle si la grille se remplit sans alignement ;
- il n'y a ni nulle par répétition ni règle des cinquante coups : chaque coup ajoute un jeton, la
  partie dure **au plus quarante-deux coups**.

La conformité est vérifiée par un test de **perft** depuis la grille vide. Les six premiers niveaux
comptent exactement 7ⁿ parties — 7, 49, 343, 2 401, 16 807, 117 649 — car rien ne peut restreindre
le choix avant le septième coup : il faut sept jetons pour remplir une colonne, et huit pour qu'un
alignement termine la partie. Au septième niveau, il en manque exactement sept : les sept lignes
qui remplissent une colonne d'entrée de jeu. Un second comptage, qui rejoue chaque ligne depuis le
début au lieu d'annuler les coups, vérifie au passage que `UnmakeMove` ne laisse rien derrière lui.

## L'IA

`SearchEngine` est un negamax avec élagage alpha-bêta :

- approfondissement itératif sous contrainte de temps ;
- table de transposition de 2²⁰ entrées indexée par hachage Zobrist ;
- exploration **des colonnes centrales vers les bords** : la colonne du milieu participe à sept
  alignements, celle du bord à trois ;
- tri des coups (coup de la table, coups tueurs) ;
- détection du gain immédiat avant toute évaluation statique, y compris à l'horizon de la
  recherche : s'arrêter juste avant un alignement disponible donnerait un score faux ;
- évaluation par **fenêtres** : chacun des 69 alignements de quatre cases vaut d'autant plus qu'on
  y a déjà posé de jetons, et ne vaut plus rien dès que les deux couleurs s'y croisent.

| Niveau | Profondeur max | Temps par coup | Aléa |
| --- | --- | --- | --- |
| Facile | 2 | 0,2 s | ±220 points |
| Moyen | 6 | 0,8 s | ±40 points |
| Difficile | 14 | 2 s | aucun |
| Expert | 42 | 6 s | aucun |

En pratique, le niveau Expert atteint la profondeur 16 ou 17 en 6 secondes depuis la grille vide,
soit environ 2,5 millions de positions par seconde ; le niveau Difficile est limité par sa
profondeur, pas par son temps — il atteint 14 en moins d'une seconde.

L'aléa n'empêche jamais de gagner : un alignement à portée de main est joué avant même que la
recherche ne commence. Une IA qui laisse passer le gain qu'elle a sous les yeux n'est pas faible,
elle est cassée.

## Organisation

```
src/Puissance4.Core    moteur : grille, coups, état de partie, évaluation, recherche
src/Puissance4.Web     interface graphique Blazor WebAssembly
src/Puissance4.Cli     interface console
tests/Puissance4.Core.Tests   69 tests (règles, perft, alignements, notation, PCN, IA)
```

`Puissance4.Core` ne dépend de rien d'autre que du framework : les deux interfaces le consomment
tel quel, et une troisième (bureau, service) n'aurait rien à réécrire.

Côté web, `GameSession` fait le pont : il tient le relevé et l'identité des jetons affichés, de
sorte qu'un jeton soit animé de sa réserve jusqu'à sa case plutôt que d'y apparaître. La grille est
faite de trois calques — le panneau percé, les jetons qui glissent derrière lui, et par-dessus les
sept colonnes cliquables, dont les biseaux redonnent aux jetons leur profondeur. Le navigateur
n'ayant qu'un fil d'exécution, les temps de réflexion y sont raccourcis et une seule boucle de jeu
de l'ordinateur tourne à la fois.

### Déploiement

Le workflow `.github/workflows/pages.yml` publie `src/Puissance4.Web` sur GitHub Pages à chaque
poussée sur `main`. Il faut que Pages soit activé sur le dépôt avec « GitHub Actions » comme
source ; sur un compte gratuit, cela suppose un dépôt public.

## Développer

```bash
dotnet build
dotnet test
```

Les positions de test s'écrivent en dessinant la grille, ce qui rend les cas de règles lisibles :

```csharp
// Les Jaunes ont le même trio que les Rouges, une rangée plus haut, mais les deux cases
// qui le complètent sont en l'air : leur alignement ne se joue pas encore.
Board board = Board.FromRows(
    ".......",
    ".......",
    ".......",
    ".......",
    "..JJJ..",
    "..RRR..");

Assert.Equal(new Move(1), MoveGenerator.WinningMove(board, Player.Red));
Assert.Null(MoveGenerator.WinningMove(board, Player.Yellow));
```

Une suite de coups s'écrit encore plus court : `Board.FromMoves("4453")`.

## Pistes

- Moteur en bitboards sur 49 bits : la grille tient dans un `ulong`, et la détection d'alignement
  se fait en quelques décalages — un ordre de grandeur en vitesse de recherche.
- Recherche à fenêtre nulle sur le seul enjeu « qui gagne, et en combien de coups », plus une
  bibliothèque d'ouvertures : le Puissance 4 est un jeu résolu, les Rouges gagnent en jouant la
  colonne 4 et en jouant parfaitement ensuite.
