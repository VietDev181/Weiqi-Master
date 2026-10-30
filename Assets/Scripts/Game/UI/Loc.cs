using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Weiqi.UI
{
    public enum Language { English = 0, Vietnamese = 1, Chinese = 2, Japanese = 3, Korean = 4, Spanish = 5, French = 6 }

    /// <summary>Tiny localisation table. Unknown keys are returned as-is, so raw strings can be passed anywhere.</summary>
    public static class Loc
    {
        const string PrefKey = "wq_lang";
        public static event Action Changed;

        /// <summary>Native names shown in the language dropdown, indexed by <see cref="Language"/>.</summary>
        public static readonly string[] NativeNames = { "English", "Tiếng Việt", "中文", "日本語", "한국어", "Español", "Français" };

        static Language current = ReadSaved();   // English by default
        public static Language Current => current;

        static Language ReadSaved()
        {
            int v = PlayerPrefs.GetInt(PrefKey, 0);
            return v >= 0 && v < NativeNames.Length ? (Language)v : Language.English;
        }

        public static void Set(Language l)
        {
            if (l == current) return;
            current = l;
            PlayerPrefs.SetInt(PrefKey, (int)l);
            Changed?.Invoke();
        }

        public static string T(string key)
        {
            return Table.TryGetValue(key, out var v) ? v[(int)current] : key;
        }

        public static string T(string key, params object[] args) => string.Format(T(key), args);

        /// <summary>Keeps a Text in sync with the current language.</summary>
        public static void Bind(Text t, string key)
        {
            var l = t.GetComponent<LocLabel>() ?? t.gameObject.AddComponent<LocLabel>();
            l.Key = key;
            l.Refresh();
        }

        static string Rules(params string[] bullets) => "•  " + string.Join("\n\n•  ", bullets);

        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            ["sub"] = new[] { "Go  ·  The art of land and liberties", "Cờ Vây  ·  Nghệ thuật của đất và khí", "围棋  ·  地与气的艺术", "囲碁  ·  地と気の芸術", "바둑  ·  집과 활로의 예술", "Go  ·  El arte del territorio y las libertades", "Go  ·  L'art du territoire et des libertés" },
            ["mode.cap"] = new[] { "GAME MODE", "CHẾ ĐỘ CHƠI", "游戏模式", "ゲームモード", "게임 모드", "MODO DE JUEGO", "MODE DE JEU" },
            ["mode.ai"] = new[] { "Vs Computer", "Chơi với máy", "人机对战", "コンピュータ対戦", "컴퓨터 대전", "Contra la máquina", "Contre l'ordinateur" },
            ["mode.human"] = new[] { "Vs Human", "Chơi với người", "双人对战", "二人対戦", "2인 대전", "Dos jugadores", "Deux joueurs" },
            ["size.cap"] = new[] { "BOARD SIZE", "KÍCH THƯỚC BÀN CỜ", "棋盘大小", "盤の大きさ", "판 크기", "TAMAÑO DEL TABLERO", "TAILLE DU PLATEAU" },
            ["level.cap"] = new[] { "COMPUTER LEVEL", "ĐỘ KHÓ CỦA MÁY", "电脑难度", "コンピュータの強さ", "컴퓨터 난이도", "NIVEL DE LA MÁQUINA", "NIVEAU DE L'ORDINATEUR" },
            ["level.easy"] = new[] { "Easy", "Dễ", "简单", "易しい", "쉬움", "Fácil", "Facile" },
            ["level.normal"] = new[] { "Normal", "Vừa", "普通", "普通", "보통", "Normal", "Normal" },
            ["level.hard"] = new[] { "Hard", "Khó", "困难", "難しい", "어려움", "Difícil", "Difficile" },
            ["side.cap"] = new[] { "YOU PLAY", "BẠN CẦM QUÂN", "你执", "あなたの石", "내 돌", "TUS FICHAS", "VOS PIERRES" },
            ["side.black"] = new[] { "Black (first)", "Đen (đi trước)", "黑棋（先手）", "黒（先手）", "흑 (선공)", "Negras (primero)", "Noir (premier)" },
            ["side.white"] = new[] { "White", "Trắng", "白棋", "白", "백", "Blancas", "Blanc" },
            ["start"] = new[] { "START GAME", "BẮT ĐẦU VÁN CỜ", "开始对局", "対局開始", "대국 시작", "EMPEZAR PARTIDA", "COMMENCER LA PARTIE" },
            ["howto"] = new[] { "How to play", "Cách chơi", "玩法", "遊び方", "게임 방법", "Cómo jugar", "Comment jouer" },
            ["music.on"] = new[] { "Music: On", "Nhạc: Bật", "音乐：开", "音楽：オン", "음악: 켜짐", "Música: Sí", "Musique : oui" },
            ["music.off"] = new[] { "Music: Off", "Nhạc: Tắt", "音乐：关", "音楽：オフ", "음악: 꺼짐", "Música: No", "Musique : non" },
            ["sfx.on"] = new[] { "Sound: On", "Âm thanh: Bật", "音效：开", "効果音：オン", "효과음: 켜짐", "Sonido: Sí", "Son : oui" },
            ["sfx.off"] = new[] { "Sound: Off", "Âm thanh: Tắt", "音效：关", "効果音：オフ", "효과음: 꺼짐", "Sonido: No", "Son : non" },
            ["rules.title"] = new[] { "HOW TO PLAY GO", "CÁCH CHƠI CỜ VÂY", "围棋玩法", "囲碁の遊び方", "바둑 규칙", "CÓMO JUGAR AL GO", "RÈGLES DU GO" },
            ["rules.ok"] = new[] { "Got it", "Đã hiểu", "明白了", "わかった", "알겠어요", "Entendido", "Compris" },
            ["undo"] = new[] { "Undo", "Đi lại", "悔棋", "一手戻す", "무르기", "Deshacer", "Annuler" },
            ["pass"] = new[] { "Pass", "Bỏ lượt", "虚着", "パス", "패스", "Pasar", "Passer" },
            ["resign"] = new[] { "Resign", "Đầu hàng", "认输", "投了", "기권", "Rendirse", "Abandonner" },
            ["menu"] = new[] { "Menu", "Menu", "菜单", "メニュー", "메뉴", "Menú", "Menu" },
            ["resume"] = new[] { "Keep playing", "Tiếp tục chơi", "继续对局", "対局を続ける", "계속하기", "Seguir jugando", "Continuer" },
            ["confirm"] = new[] { "Confirm result", "Xác nhận kết quả", "确认结果", "結果を確定", "결과 확정", "Confirmar resultado", "Confirmer le résultat" },
            ["viewboard"] = new[] { "View board", "Xem bàn cờ", "查看棋盘", "盤面を見る", "판 보기", "Ver tablero", "Voir le plateau" },
            ["tomenu"] = new[] { "Main menu", "Về menu", "返回菜单", "メニューへ", "메뉴로", "Menú principal", "Menu principal" },
            ["newgame"] = new[] { "New game", "Ván mới", "新对局", "新しい対局", "새 대국", "Nueva partida", "Nouvelle partie" },
            ["you"] = new[] { "You", "Bạn", "你", "あなた", "나", "Tú", "Vous" },
            ["cpu"] = new[] { "Computer · {0}", "Máy · {0}", "电脑 · {0}", "コンピュータ · {0}", "컴퓨터 · {0}", "Máquina · {0}", "Ordinateur · {0}" },
            ["p1"] = new[] { "Player 1", "Người chơi 1", "玩家 1", "プレイヤー1", "플레이어 1", "Jugador 1", "Joueur 1" },
            ["p2"] = new[] { "Player 2", "Người chơi 2", "玩家 2", "プレイヤー2", "플레이어 2", "Jugador 2", "Joueur 2" },
            ["captured"] = new[] { "Captured: {0}", "Bắt được: {0}", "提子：{0}", "取った石：{0}", "따낸 돌: {0}", "Capturadas: {0}", "Capturées : {0}" },
            ["komi"] = new[] { "Komi +{0}", "Komi +{0}", "贴目 +{0}", "コミ +{0}", "덤 +{0}", "Komi +{0}", "Komi +{0}" },
            ["black"] = new[] { "Black", "Đen", "黑", "黒", "흑", "Negras", "Noir" },
            ["white"] = new[] { "White", "Trắng", "白", "白", "백", "Blancas", "Blanc" },
            ["err.occupied"] = new[] { "That point is already taken", "Điểm này đã có quân", "此处已有棋子", "すでに石があります", "이미 돌이 있습니다", "Ese punto ya está ocupado", "Ce point est déjà occupé" },
            ["err.suicide"] = new[] { "Suicide move — not allowed", "Nước tự sát — không hợp lệ", "自杀着 — 不允许", "自殺手は打てません", "자충수는 둘 수 없습니다", "Suicidio: movimiento no permitido", "Suicide : coup interdit" },
            ["err.ko"] = new[] { "Ko rule — you cannot repeat a position", "Luật Ko — không được lặp lại thế cờ", "打劫规则 — 不能重复局面", "コウのルール — 同じ局面は作れません", "패 규칙 — 같은 국면을 반복할 수 없습니다", "Regla del Ko: no se puede repetir la posición", "Règle du ko : position déjà vue" },
            ["err.generic"] = new[] { "Illegal move", "Nước đi không hợp lệ", "非法落子", "無効な着手", "둘 수 없는 수", "Movimiento ilegal", "Coup illégal" },
            ["st.scoring"] = new[] { "Tap dead stones to mark them  ·  Black {0} — White {1}", "Chạm vào quân chết để đánh dấu  ·  Đen {0} — Trắng {1}", "点击死子进行标记  ·  黑 {0} — 白 {1}", "死に石をタップして印を付ける  ·  黒 {0} — 白 {1}", "죽은 돌을 눌러 표시하세요  ·  흑 {0} — 백 {1}", "Toca las piedras muertas  ·  Negras {0} — Blancas {1}", "Touchez les pierres mortes  ·  Noir {0} — Blanc {1}" },
            ["st.finished"] = new[] { "The game has ended", "Ván cờ đã kết thúc", "对局结束", "対局終了", "대국 종료", "La partida ha terminado", "La partie est terminée" },
            ["st.thinking"] = new[] { "Computer is thinking", "Máy đang suy nghĩ", "电脑思考中", "コンピュータが考え中", "컴퓨터가 생각 중", "La máquina está pensando", "L'ordinateur réfléchit" },
            ["st.yourturn"] = new[] { "Your turn  ·  {0} stones", "Lượt của bạn  ·  quân {0}", "轮到你了  ·  执{0}", "あなたの番  ·  {0}", "내 차례  ·  {0}", "Tu turno  ·  {0}", "À vous  ·  {0}" },
            ["st.turn"] = new[] { "{0} to play", "Lượt của {0}", "轮到{0}下", "{0}の番", "{0} 차례", "Turno de {0}", "Au tour de {0}" },
            ["st.passed"] = new[] { "  ·  opponent passed", "  ·  đối thủ đã bỏ lượt", "  ·  对方已虚着", "  ·  相手がパスしました", "  ·  상대가 패스했습니다", "  ·  el rival pasó", "  ·  l'adversaire a passé" },
            ["res.win"] = new[] { "YOU WIN!", "BẠN THẮNG!", "你赢了！", "あなたの勝ち！", "당신이 이겼습니다!", "¡HAS GANADO!", "VOUS AVEZ GAGNÉ !" },
            ["res.lose"] = new[] { "COMPUTER WINS", "MÁY THẮNG", "电脑获胜", "コンピュータの勝ち", "컴퓨터 승리", "GANA LA MÁQUINA", "L'ORDINATEUR GAGNE" },
            ["res.colorwins"] = new[] { "{0} WINS!", "{0} THẮNG!", "{0}获胜！", "{0}の勝ち！", "{0} 승리!", "¡VICTORIA DE {0}!", "{0} GAGNE !" },
            ["res.resigned"] = new[] { "{0} resigned.", "{0} đã đầu hàng.", "{0}认输了。", "{0}が投了しました。", "{0}이(가) 기권했습니다.", "{0} se ha rendido.", "{0} a abandonné." },
            ["res.score"] = new[] { "Black  {0}  —  White  {1} + {2} komi = {3}", "Đen  {0}  —  Trắng  {1} + {2} komi = {3}", "黑 {0}  —  白 {1} + 贴目 {2} = {3}", "黒 {0}  —  白 {1} + コミ {2} = {3}", "흑 {0}  —  백 {1} + 덤 {2} = {3}", "Negras {0}  —  Blancas {1} + {2} komi = {3}", "Noir {0}  —  Blanc {1} + {2} komi = {3}" },
            ["res.margin"] = new[] { "{0} wins by {1} points", "{0} thắng {1} điểm", "{0}胜{1}目", "{0}の{1}目勝ち", "{0} {1}집 승", "{0} gana por {1} puntos", "{0} gagne de {1} points" },
            ["est"] = new[] { "Est. score: {0}", "Điểm tạm tính: {0}", "估算得分：{0}", "推定得点：{0}", "예상 점수: {0}", "Puntos est.: {0}", "Score estimé : {0}" },
            ["terr"] = new[] { "Territory", "Lãnh thổ", "地盘", "地", "집 표시", "Territorio", "Territoire" },
            ["rules.body"] = new[]
            {
                Rules(
                    "Two players take turns placing <b>black</b> and <b>white</b> stones on the intersections. Stones never move once placed.",
                    "A stone or connected group has empty neighbouring points called <b>liberties</b>. With none left, it is captured and removed.",
                    "You may not play a suicide move (unless it captures) nor repeat an earlier position (the <b>Ko rule</b>).",
                    "Balance expanding territory, protecting your stones and attacking the opponent — sometimes sacrifice a small group for a bigger gain.",
                    "When both players pass, the game moves to <b>scoring</b>: tap dead stones to mark them. Score = living stones + surrounded territory; White gets komi as compensation for moving second.",
                    "The higher score wins."),
                Rules(
                    "Hai người lần lượt đặt quân <b>đen</b> và <b>trắng</b> lên các giao điểm. Quân đã đặt không tự di chuyển.",
                    "Mỗi quân hoặc nhóm quân có các điểm trống liền kề gọi là <b>khí</b>. Hết khí là bị bắt và nhấc khỏi bàn.",
                    "Không được đi vào điểm tự sát (trừ khi bắt được quân đối phương) và không được lặp lại thế cờ cũ (<b>luật Ko</b>).",
                    "Cân bằng giữa mở rộng lãnh thổ, bảo vệ quân, tấn công đối thủ — đôi khi hy sinh nhóm nhỏ để lấy lợi thế lớn.",
                    "Khi cả hai cùng bỏ lượt, ván cờ chuyển sang <b>tính điểm</b>: chạm vào quân chết để đánh dấu. Điểm = quân còn sống + lãnh thổ vây được; Trắng được cộng komi bù cho việc đi sau.",
                    "Ai nhiều điểm hơn sẽ thắng."),
                Rules(
                    "双方轮流在交叉点上落下<b>黑子</b>和<b>白子</b>，落子后不能移动。",
                    "一个棋子或相连的一块棋，其相邻的空点称为<b>气</b>。气被全部堵住，这块棋就会被提走。",
                    "不能下自杀的着法（除非能提掉对方棋子），也不能让局面重复（<b>打劫规则</b>）。",
                    "在扩大地盘、保护己方棋子和攻击对手之间取得平衡——有时舍弃小块棋以换取更大的优势。",
                    "双方连续虚着后进入<b>数子阶段</b>：点击死子进行标记。得分 = 活子 + 围住的地；白棋获得贴目，作为后手的补偿。",
                    "得分高者获胜。"),
                Rules(
                    "二人が交互に<b>黒</b>と<b>白</b>の石を交点に置きます。置いた石は動きません。",
                    "石や連なった石の隣にある空き点を<b>呼吸点（気）</b>といいます。気がなくなると取られて盤上から取り除かれます。",
                    "自殺手（相手の石を取れる場合を除く）や、以前と同じ局面にする手（<b>コウのルール</b>）は打てません。",
                    "陣地の拡大、石の防衛、相手への攻めのバランスをとり、時には小さな石を捨てて大きな利を得ましょう。",
                    "両者が続けてパスすると<b>計算</b>に進みます。死に石をタップして印を付けてください。得点 = 生きている石 + 囲った地。白は後手のぶんコミが加算されます。",
                    "得点の多い方が勝ちです。"),
                Rules(
                    "두 사람이 번갈아 <b>흑돌</b>과 <b>백돌</b>을 교차점에 놓습니다. 한번 놓은 돌은 움직이지 않습니다.",
                    "돌이나 이어진 돌 무리에 인접한 빈 점을 <b>활로(호흡)</b>라고 합니다. 활로가 모두 사라지면 따내어 판에서 제거됩니다.",
                    "자충수(상대 돌을 따내는 경우는 제외)와 이전 국면을 반복하는 수(<b>패 규칙</b>)는 둘 수 없습니다.",
                    "집을 넓히고, 돌을 지키고, 상대를 공격하는 균형을 맞추세요. 때로는 작은 돌을 버려 더 큰 이득을 얻기도 합니다.",
                    "두 사람이 연달아 패스하면 <b>계가</b>로 넘어갑니다. 죽은 돌을 눌러 표시하세요. 점수 = 산 돌 + 둘러싼 집. 백은 후수 보상으로 덤을 받습니다.",
                    "점수가 높은 쪽이 이깁니다."),
                Rules(
                    "Dos jugadores colocan por turnos piedras <b>negras</b> y <b>blancas</b> en las intersecciones. Las piedras no se mueven una vez colocadas.",
                    "Una piedra o grupo conectado tiene puntos vacíos adyacentes llamados <b>libertades</b>. Sin libertades, el grupo es capturado y retirado.",
                    "No se puede jugar un suicidio (salvo que capture) ni repetir una posición anterior (<b>regla del Ko</b>).",
                    "Equilibra expandir territorio, proteger tus piedras y atacar al rival; a veces conviene sacrificar un grupo pequeño por una ventaja mayor.",
                    "Cuando ambos pasan, empieza el <b>recuento</b>: toca las piedras muertas para marcarlas. Puntos = piedras vivas + territorio rodeado; las Blancas reciben komi por mover en segundo lugar.",
                    "Gana quien tenga más puntos."),
                Rules(
                    "Deux joueurs posent à tour de rôle des pierres <b>noires</b> et <b>blanches</b> sur les intersections. Une pierre posée ne bouge plus.",
                    "Une pierre ou un groupe connecté possède des points vides adjacents appelés <b>libertés</b>. Sans libertés, il est capturé et retiré.",
                    "Il est interdit de jouer un suicide (sauf s'il capture) ou de répéter une position précédente (<b>règle du ko</b>).",
                    "Équilibrez l'extension du territoire, la protection de vos pierres et l'attaque de l'adversaire — sacrifiez parfois un petit groupe pour un gain plus grand.",
                    "Quand les deux joueurs passent, on passe au <b>décompte</b> : touchez les pierres mortes pour les marquer. Score = pierres vivantes + territoire entouré ; Blanc reçoit le komi pour jouer en second.",
                    "Le score le plus élevé l'emporte.")
            },
        };
    }

    /// <summary>Re-applies a localisation key whenever the language changes.</summary>
    public sealed class LocLabel : MonoBehaviour
    {
        public string Key;
        Text text;

        void OnEnable() { Loc.Changed += Refresh; Refresh(); }
        void OnDisable() { Loc.Changed -= Refresh; }

        public void Refresh()
        {
            if (text == null) text = GetComponent<Text>();
            if (text != null && !string.IsNullOrEmpty(Key)) text.text = Loc.T(Key);
        }
    }
}
