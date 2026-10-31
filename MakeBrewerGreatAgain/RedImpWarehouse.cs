using System;
using System.Collections.Generic;
using UnityEngine;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 田所浩二：红魔鬼酿酒厂灌装线上的工人，私人电话 114514。
    ///
    /// 游戏里的电话是"号码 → StorePhoneClient → dialogFuncId → 对话函数"的结构：
    ///   - 号码表是 PlayerStore.Instance.PhoneClientDict（随存档保存）；
    ///   - 对话函数表是 static PhoneClientList.DialogFuncs（按 dialogFuncId 查）。
    /// 所以这里直接往两张表里各注册一条，对话完全由我们自己搭，不改原版红魔鬼代表的任何选项。
    ///
    /// 买下"空瓶回收项目"之后：建号 + 发一张他的名片。
    /// </summary>
    internal static class RedImpWarehouse
    {
        /// <summary>工人的私人号码。</summary>
        public const long PhoneNumber = 114514L;

        /// <summary>对话函数在 PhoneClientList.DialogFuncs 里的键。</summary>
        public const string DialogFuncId = "gmtb_red_imp_warehouse";

        /// <summary>名片物品 id（在 ModItemDirectory 里注册）。</summary>
        public const string CardItemId = "red_imp_warehouse_card";

        public const string DisplayName = "田所浩二";

        private const int CooldownDays = 2;

        private const string OrderChoice = "订购8个空瓶（次日送达）";
        private const string BrewChoice = "我要怎么用这些脏瓶子酿酒？";

        // ------------------------------------------------------------------ 注册
        /// <summary>买下升级后调用：注册号码与对话、发一张名片。重复调用安全。</summary>
        public static void EnsureProgram()
        {
            EnsureDialogFunc();

            bool created = EnsureClient();
            if (created) SpawnCard();
        }

        /// <summary>模组被停用时把号码和对话撤掉，避免留下指向已卸载代码的委托。</summary>
        public static void Unregister()
        {
            try
            {
                var dict = PhoneClientList.DialogFuncs;
                if (dict != null) dict.Remove(DialogFuncId);
            }
            catch (Exception)
            {
            }

            try
            {
                PlayerStore store = PlayerStore.Instance;
                if (store != null && store.PhoneClientDict != null)
                    store.PhoneClientDict.Remove(PhoneNumber);
            }
            catch (Exception)
            {
            }
        }

        private static void EnsureDialogFunc()
        {
            try
            {
                var dict = PhoneClientList.DialogFuncs;
                if (dict == null) return;
                if (dict.ContainsKey(DialogFuncId)) return;

                dict[DialogFuncId] = client => BuildDialog(client);
            }
            catch (Exception e)
            {
                Warn("注册田所浩二电话对话失败：" + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>把 114514 塞进本局的号码表；返回是否新建。</summary>
        private static bool EnsureClient()
        {
            try
            {
                PlayerStore store = PlayerStore.Instance;
                if (store == null || store.PhoneClientDict == null) return false;
                if (store.PhoneClientDict.ContainsKey(PhoneNumber)) return false;

                store.PhoneClientDict[PhoneNumber] = new StorePhoneClient
                {
                    displayName = DisplayName,
                    // 兜底用原版已有的客户名字键（"红魔鬼快递员"）：万一名字补丁被内联绕过，
                    // 通讯录里也不会出现空行或原始键名；正常情况下 WarehousePhoneNamePatch 会改成"田所浩二"。
                    locID = "name_red_imp_courrier",
                    dialogFuncId = DialogFuncId,
                    phoneClientType = StorePhoneClient.PhoneClientType.None,
                    phoneState = StorePhoneClient.PhoneState.Regular,
                    cooldownDuration = CooldownDays,
                    currentCooldown = 0,
                    dialedBefore = false,
                    alias = new List<string>(),
                };

                return true;
            }
            catch (Exception e)
            {
                Warn("创建田所浩二的电话失败：" + e.GetType().Name + ": " + e.Message);
                return false;
            }
        }

        // ------------------------------------------------------------------ 名片
        /// <summary>名片物品图标（Textures/icons/card.png，32x16，和原版名片一样大）。</summary>
        private const string CardIconPath = "Textures/icons/card.png";

        /// <summary>展开阅读时显示的卡片图（Textures/Paper/114514.png，由 GameLoader 作为 PaperSprites 载入）。</summary>
        private const string CardPaperPath = "Textures/Paper/114514.png";

        // 纸张精灵在 LoadedMod.PaperSprites 里的键带 "Paper/" 前缀（游戏就是这么存的）
        private const string CardPaperKey = "Paper/114514";

        /// <summary>纸质手册（原版纸张外框 + 我们自己的卡片图）。</summary>
        private const string CardBookId = "114514";

        private const string CardBookPath = "Books/114514.json";

        private const string CardWindowId = "make_brewer_great_again.card";

        // 原版纸张美术都是按 2 倍显示的（笔记本背景 314x402 -> 628x804），名片图也照 2 倍放
        private const float CardImageWidth = 600f;
        private const float CardImageHeight = 360f;
        private const float CardWindowWidth = 640f;
        private const float CardWindowHeight = 470f;

        /// <summary>名片物品（复刻原版名片：DOCUMENT + 重要标签 + 纸板音效，读它打开卡片界面）。</summary>
        public static GameItem CreateCard()
        {
            GameItem card = ItemDirectory.CreateEmptyItem(CardItemId);

            // 用你自己画的名片图标；没有这张图时退回原版名片贴图
            if (!TryUseModCardSprite(card))
                card.SetSpriteAndShape("Items/items_misc", "card_brewer");

            card.name = "名片（田所浩二）";
            card.flavorText = "114514";
            card.shortDescription = "一条被撕下来的标签，背面用潦草的字迹写着一串数字";
            card.unitValue = 0;
            card.SetGameItemType("DOCUMENT");
            card.EnableTag("IMPORTANT_TAG", false);
            AudioHelper.InitCartonCard(card);

            card.canActivateSlotItemFunc = (item, inventory, slot) => true;
            card.mayActivateSlotItemFunc = (item, inventory, slot) => true;
            card.onActivateSlotItemFunc = (item, inventory, slot) => OpenCardPaper();

            return card;
        }

        /// <summary>你画的名片图标（Textures/icons/card.png）存在就用它，否则退回原版贴图。</summary>
        private static bool TryUseModCardSprite(GameItem card)
        {
            try
            {
                string modId = ModId();
                if (string.IsNullOrEmpty(modId)) return false;
                if (!ModHelper.ModFileExists(modId, CardIconPath)) return false;

                card.SetSpriteAndShapeFromMod(modId, ModIcons.CardKey);
                return true;
            }
            catch (Exception)
            {
            }

            return false;
        }

        /// <summary>把原版名片贴图的像素尺寸打到 F8，方便照着画替换图。</summary>
        private static void LogCardSpriteSize()
        {
            try
            {
                Sprite sprite = RenderHandler.LoadFromAtlas("Items/items_misc", "card_brewer");
                if (sprite == null) return;

                Rect rect = sprite.textureRect;
                Warn("原版名片贴图 card_brewer = " + Mathf.RoundToInt(rect.width) + " x " + Mathf.RoundToInt(rect.height) + " 像素。");
            }
            catch (Exception)
            {
            }
        }

        /// <summary>右键阅读：优先用原版纸质手册（Books/114514.json，带原版纸张外框），没配就用自定义窗口，再退回文字纸页。</summary>
        private static void OpenCardPaper()
        {
            if (TryOpenCardBook()) return;
            if (TryOpenCardWindow()) return;

            try
            {
                PaperUIManager.Instance.OpenUI(
                    "名片（田所浩二）",
                    "田所浩二\n红魔鬼酿酒厂 · 灌装线\n电话：114514\n\n（背面潦草地写着：瓶子用之前拿水冲一冲）",
                    "合理损耗，不算偷。",
                    1);
            }
            catch (Exception e)
            {
                Warn("打开名片失败：" + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>用模组自己的纸质手册（Books/114514.json，背景就是 Textures/Paper/114514.png）。</summary>
        private static bool TryOpenCardBook()
        {
            try
            {
                string modId = ModId();
                if (string.IsNullOrEmpty(modId)) return false;
                if (!ModHelper.ModFileExists(modId, CardBookPath)) return false;

                return ModHelper.OpenPaper(modId, CardBookId) != null;
            }
            catch (Exception e)
            {
                Warn("打开名片手册失败：" + e.GetType().Name + ": " + e.Message);
                return false;
            }
        }

        /// <summary>用自定义窗口把卡片图原样显示出来（保持 5:3 比例，不拉伸）。</summary>
        private static bool TryOpenCardWindow()
        {
            try
            {
                if (CustomUIManager.Instance == null) return false;

                string modId = ModId();
                if (string.IsNullOrEmpty(modId)) return false;
                if (!ModHelper.ModFileExists(modId, CardPaperPath)) return false;

                CustomUIBuilder builder = CustomUIManager.Window(CardWindowId, "名片（田所浩二）", CustomUIManager.LAYER_OVERLAY)
                    .SetSize(CardWindowWidth, CardWindowHeight)
                    .Center()
                    .SetCloseOnEscape(true);

                builder.AddImage(modId + ":" + CardPaperKey, CardImageWidth, CardImageHeight, "card");
                builder.AddFlexibleSpace().AddButton("关闭", CloseCardWindow, null);

                builder.Show();
                return true;
            }
            catch (Exception e)
            {
                Warn("打开名片图失败：" + e.GetType().Name + ": " + e.Message);
                return false;
            }
        }

        private static void CloseCardWindow()
        {
            try
            {
                if (CustomUIManager.Instance != null) CustomUIManager.Instance.CloseWindow(CardWindowId);
            }
            catch (Exception)
            {
            }
        }

        private static void SpawnCard()
        {
            try
            {
                GameItem card = DirectoryMaster.Item(CardItemId, true);
                if (card == null) return;
                GraphUtils.EmporiumTryAdd(card);

                LogCardSpriteSize();
            }
            catch (Exception e)
            {
                Warn("发放田所浩二的名片失败：" + e.GetType().Name + ": " + e.Message);
            }
        }

        // ------------------------------------------------------------------ 对话
        private static Dialogue BuildDialog(StorePhoneClient client)
        {
            if (client != null && client.phoneState == StorePhoneClient.PhoneState.Cooldown)
                return BuildCooldownDialog(client);

            return BuildOrderDialog(client);
        }

        private static Dialogue BuildOrderDialog(StorePhoneClient client)
        {
            Dialogue root = new Dialogue();
            root.SetText(DisplayName, "喂？……王尔德介绍的？行，你说。");

            Dialogue menu = new Dialogue();
            menu.SetText(DisplayName, "今天工厂”损耗“了不少空瓶子，你要多少？");
            root.SetNextDialogue(menu);

            // 订购
            Dialogue orderLine = new Dialogue();
            orderLine.SetText("PLAYER", OrderChoice);

            Dialogue orderReply = new Dialogue();
            orderReply.SetText(DisplayName, "八个旧瓶，明天让快递员给你捎过去，钱直接给他。");
            orderReply.SetEndAction(() => PhoneUIManager.Instance.OnPhoneDialogEnd(true));
            orderLine.SetNextDialogue(orderReply);

            menu.AddChoice(
                DirectoryMaster.Item("$", true),
                OrderChoice,
                "田所浩二会从厂里的\"合理损耗\"里扣 8 个用过的空瓶，明天由快递员送来。",
                orderLine,
                () => Order(client),
                null);

            // 怎么酿酒
            Dialogue brewLine = new Dialogue();
            brewLine.SetText("PLAYER", BrewChoice);

            Dialogue brewReply = new Dialogue();
            brewReply.SetText(DisplayName, "拿到水龙头底下冲一冲不就行了，酿酒厂也是这么干的。");
            brewReply.SetNextDialogue(menu);
            brewLine.SetNextDialogue(brewReply);

            menu.AddChoice(
                DirectoryMaster.Item("$", true),
                BrewChoice,
                "问问他这些脏瓶子该怎么处理才能酿酒。",
                brewLine,
                () => { },
                null);

            // 结束通话
            Dialogue quit = new Dialogue();
            quit.SetText(DisplayName, LocHelper.GetLocalizedDialoguePhone("end_call"));
            quit.SetEndAction(() => PhoneUIManager.Instance.OnPhoneDialogEnd(true));

            menu.AddChoice(
                DirectoryMaster.Item("$", true),
                "",
                "",
                quit,
                () => { },
                null);

            return root;
        }

        private static Dialogue BuildCooldownDialog(StorePhoneClient client)
        {
            Dialogue root = new Dialogue();
            root.SetText(DisplayName, "今天不行，厂里有人盯着。");

            Dialogue line3 = new Dialogue();
            line3.SetText(DisplayName, "到日子再打过来。");
            line3.SetEndAction(() => PhoneUIManager.Instance.OnPhoneDialogEnd(true));
            root.SetNextDialogue(line3);

            return root;
        }

        // ------------------------------------------------------------------ 下单
        private static void Order(StorePhoneClient client)
        {
            try
            {
                if (client != null)
                {
                    // 进入 2 天冷却，期间再打过来只会听到"今天不行"
                    client.cooldownDuration = CooldownDays;
                    client.UseService();
                }

                BottleDelivery.QueueBottleDelivery();
            }
            catch (Exception e)
            {
                Warn("订购空瓶失败：" + e.GetType().Name + ": " + e.Message);
            }
        }

        private static string ModId()
        {
            try
            {
                if (ModEntry.Instance != null && ModEntry.Instance.Manifest != null)
                    return ModEntry.Instance.Manifest.ID;
            }
            catch (Exception)
            {
            }

            return "make_brewer_great_again";
        }

        private static void Warn(string message)
        {
            if (ModEntry.Instance != null && ModEntry.Instance.Log != null)
                ModEntry.Instance.Log.Warning(message);
        }
    }
}
