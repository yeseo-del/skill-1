﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;
using POESKillTree.ViewModels;
using ItemClass = POESKillTree.ViewModels.ItemAttributes.Item.ItemClass;

namespace POESKillTree.SkillTreeFiles
{
    public partial class SkillTree
    {
        public delegate void UpdateLoadingWindow(double current, double max);

        public delegate void CloseLoadingWindow();

        public delegate void StartLoadingWindow();


        public static float LifePerLevel = 12;
        public static float AccPerLevel = 2;
        public static float EvasPerLevel = 3;
        public static float ManaPerLevel = 4;
        public static float IntPerMana = 2;
        public static float IntPerES = 5; //%
        public static float StrPerLife = 2;
        public static float StrPerED = 5; //%
        public static float DexPerAcc = 0.5f;
        public static float DexPerEvas = 5; //%
        private static Action _emptyDelegate = delegate { };
        private readonly Dictionary<string, Asset> _assets = new Dictionary<string, Asset>();
        public List<string> AttributeTypes = new List<string>();
        public HashSet<ushort> AvailNodes = new HashSet<ushort>();

        public Dictionary<string, float> BaseAttributes = new Dictionary<string, float>
        {
            {"+# to maximum Mana", 36},
            {"+# to maximum Life", 38},
            {"Evasion Rating: #", 53},
            {"+# Maximum Endurance Charge", 3},
            {"+# Maximum Frenzy Charge", 3},
            {"+# Maximum Power Charge", 3},
            {"#% Additional Elemental Resistance per Endurance Charge", 4},
            {"#% Physical Damage Reduction per Endurance Charge", 4},
            {"#% Attack Speed Increase per Frenzy Charge", 5},
            {"#% Cast Speed Increase per Frenzy Charge", 5},
            {"#% Critical Strike Chance Increase per Power Charge", 50},
        };

        public Dictionary<string, float>[] CharBaseAttributes = new Dictionary<string, float>[7];

        public List<string> CharName = new List<string>
        {
            "SEVEN",
            "MARAUDER",
            "RANGER",
            "WITCH",
            "DUELIST",
            "TEMPLAR",
            "SIX"
        };

        public List<string> FaceNames = new List<string>
        {
            "centerscion",
            "centermarauder",
            "centerranger",
            "centerwitch",
            "centerduelist",
            "centertemplar",
            "centershadow"
        };

        public HashSet<int[]> Links = new HashSet<int[]>();
        public List<SkillNodeGroup> NodeGroups = new List<SkillNodeGroup>();
        public HashSet<ushort> SkilledNodes = new HashSet<ushort>();
        public Dictionary<UInt16, SkillNode> Skillnodes = new Dictionary<UInt16, SkillNode>();

        public Rect2D TRect = new Rect2D();
        private const string TreeAddress = "http://www.pathofexile.com/passive-skill-tree/";
        private int _chartype;
        private List<SkillNode> _highlightnodes;
        public SkillIcons IconActiveSkills = new SkillIcons();
        public SkillIcons IconInActiveSkills = new SkillIcons();
        public int _level = 1;

        public Dictionary<string, string> NodeBackgrounds = new Dictionary<string, string>
        {
            {"normal", "PSSkillFrame"},
            {"notable", "NotableFrameUnallocated"},
            {"keystone", "KeystoneFrameUnallocated"}
        };

        public Dictionary<string, string> NodeBackgroundsActive = new Dictionary<string, string>
        {
            {"normal", "PSSkillFrameActive"},
            {"notable", "NotableFrameAllocated"},
            {"keystone", "KeystoneFrameAllocated"}
        };

        public float ScaleFactor = 1;

        public SkillTree(String treestring, bool displayProgress, UpdateLoadingWindow update)
        {
            var jss = new JsonSerializerSettings
            {
                Error = delegate(object sender, ErrorEventArgs args)
                {
                    Debug.WriteLine(args.ErrorContext.Error.Message);
                    args.ErrorContext.Handled = true;
                }
            };

            var inTree = JsonConvert.DeserializeObject<PoESkillTree>(treestring.Replace("Additional ", ""), jss);
            int qindex = 0;


            foreach (var obj in inTree.skillSprites)
            {
                if (obj.Key.Contains("inactive"))
                    continue;
                IconActiveSkills.Images[obj.Value[3].filename] = null;
                foreach (var o in obj.Value[3].coords)
                {
                    IconActiveSkills.SkillPositions[o.Key] =
                        new KeyValuePair<Rect, string>(new Rect(o.Value.x, o.Value.y, o.Value.w, o.Value.h),
                            obj.Value[3].filename);
                }
            }
            foreach (var obj in inTree.skillSprites)
            {
                if (obj.Key.Contains("active"))
                    continue;
                IconActiveSkills.Images[obj.Value[3].filename] = null;
                foreach (var o in obj.Value[3].coords)
                {
                    IconActiveSkills.SkillPositions[o.Key] =
                        new KeyValuePair<Rect, string>(new Rect(o.Value.x, o.Value.y, o.Value.w, o.Value.h),
                            obj.Value[3].filename);
                }
            }

            foreach (var ass in inTree.assets)
            {
                _assets[ass.Key] = new Asset(ass.Key,
                    ass.Value.ContainsKey(0.3835f) ? ass.Value[0.3835f] : ass.Value.Values.First());
            }

            if (displayProgress)
                update(50, 100);
            IconActiveSkills.OpenOrDownloadImages(update);
            if (displayProgress)
                update(75, 100);
            IconInActiveSkills.OpenOrDownloadImages(update);
            foreach (var c in inTree.characterData)
            {
                CharBaseAttributes[c.Key] = new Dictionary<string, float>
                {
                    {"+# to Strength", c.Value.base_str},
                    {"+# to Dexterity", c.Value.base_dex},
                    {"+# to Intelligence", c.Value.base_int}
                };
            }
            foreach (Node nd in inTree.nodes)
            {
                Skillnodes.Add(nd.id, new SkillNode
                {
                    id = nd.id,
                    name = nd.dn,
                    attributes = nd.sd,
                    orbit = nd.o,
                    orbitIndex = nd.oidx,
                    icon = nd.icon,
                    linkID = nd.ot,
                    g = nd.g,
                    da = nd.da,
                    ia = nd.ia,
                    ks = nd.ks,
                    m = nd.m,
                    not = nd.not,
                    sa = nd.sa,
                    Mastery = nd.m,
                    spc = nd.spc.Count() > 0 ? (int?) nd.spc[0] : null
                });
            }
            var links = new List<ushort[]>();
            foreach (var skillNode in Skillnodes)
            {
                foreach (ushort i in skillNode.Value.linkID)
                {
                    if (
                        links.Count(
                            nd => (nd[0] == i && nd[1] == skillNode.Key) || nd[0] == skillNode.Key && nd[1] == i) ==
                        1)
                    {
                        continue;
                    }
                    links.Add(new[] {skillNode.Key, i});
                }
            }
            foreach (var ints in links)
            {
                if (!Skillnodes[ints[0]].Neighbor.Contains(Skillnodes[ints[1]]))
                    Skillnodes[ints[0]].Neighbor.Add(Skillnodes[ints[1]]);
                if (!Skillnodes[ints[1]].Neighbor.Contains(Skillnodes[ints[0]]))
                    Skillnodes[ints[1]].Neighbor.Add(Skillnodes[ints[0]]);
            }

            foreach (var gp in inTree.groups)
            {
                var ng = new SkillNodeGroup();

                ng.OcpOrb = gp.Value.oo;
                ng.Position = new Vector2D(gp.Value.x, gp.Value.y);
                ng.Nodes = gp.Value.n;
                NodeGroups.Add(ng);
            }

            foreach (SkillNodeGroup group in NodeGroups)
            {
                foreach (ushort node in group.Nodes)
                {
                    Skillnodes[node].SkillNodeGroup = group;
                }
            }
            TRect = new Rect2D(new Vector2D(inTree.min_x * 1.1, inTree.min_y * 1.1),
                new Vector2D(inTree.max_x * 1.1, inTree.max_y * 1.1));


            InitNodeSurround();
            DrawNodeSurround();
            DrawNodeBaseSurround();
            DrawSkillIconLayer();
            DrawBackgroundLayer();
            InitFaceBrushesAndLayer();
            DrawLinkBackgroundLayer(links);
            InitOtherDynamicLayers();
            CreateCombineVisual();


            var regexAttrib = new Regex("[0-9]*\\.?[0-9]+");
            foreach (var skillNode in Skillnodes)
            {
                skillNode.Value.Attributes = new Dictionary<string, List<float>>();
                foreach (string s in skillNode.Value.attributes)
                {
                    var values = new List<float>();

                    foreach (Match m in regexAttrib.Matches(s))
                    {
                        if (!AttributeTypes.Contains(regexAttrib.Replace(s, "#")))
                            AttributeTypes.Add(regexAttrib.Replace(s, "#"));
                        if (m.Value == "")
                            values.Add(float.NaN);
                        else
                            values.Add(float.Parse(m.Value, CultureInfo.InvariantCulture));
                    }
                    string cs = (regexAttrib.Replace(s, "#"));

                    skillNode.Value.Attributes[cs] = values;
                }
            }
            if (displayProgress)
                update(100, 100);
        }

        public int Level
        {
            get { return _level; }
            set { _level = value; }
        }

        public int Chartype
        {
            get { return _chartype; }
            set
            {
                _chartype = value;
                SkilledNodes.Clear();
                KeyValuePair<ushort, SkillNode> node =
                    Skillnodes.First(nd => nd.Value.name.ToUpper() == CharName[_chartype]);
                SkilledNodes.Add(node.Value.id);
                UpdateAvailNodes();
                DrawFaces();
            }
        }

        public Dictionary<string, List<float>> SelectedAttributes
        {
            get
            {
                Dictionary<string, List<float>> temp = SelectedAttributesWithoutImplicit;

                foreach (var a in ImplicitAttributes(temp))
                {
                    if (!temp.ContainsKey(a.Key))
                        temp[a.Key] = new List<float>();
                    for (int i = 0; i < a.Value.Count; i++)
                    {
                        if (temp.ContainsKey(a.Key) && temp[a.Key].Count > i)
                            temp[a.Key][i] += a.Value[i];
                        else
                        {
                            temp[a.Key].Add(a.Value[i]);
                        }
                    }
                }
                return temp;
            }
        }

        public Dictionary<string, List<float>> SelectedAttributesWithoutImplicit
        {
            get
            {
                var temp = new Dictionary<string, List<float>>();
                foreach (var attr in CharBaseAttributes[Chartype])
                {
                    if (!temp.ContainsKey(attr.Key))
                        temp[attr.Key] = new List<float>();

                    if (temp.ContainsKey(attr.Key) && temp[attr.Key].Count > 0)
                        temp[attr.Key][0] += attr.Value;
                    else
                    {
                        temp[attr.Key].Add(attr.Value);
                    }
                }

                foreach (var attr in BaseAttributes)
                {
                    if (!temp.ContainsKey(attr.Key))
                        temp[attr.Key] = new List<float>();

                    if (temp.ContainsKey(attr.Key) && temp[attr.Key].Count > 0)
                        temp[attr.Key][0] += attr.Value;
                    else
                    {
                        temp[attr.Key].Add(attr.Value);
                    }
                }

                foreach (ushort inode in SkilledNodes)
                {
                    SkillNode node = Skillnodes[inode];
                    foreach (var attr in node.Attributes)
                    {
                        if (!temp.ContainsKey(attr.Key))
                            temp[attr.Key] = new List<float>();
                        for (int i = 0; i < attr.Value.Count; i++)
                        {
                            if (temp.ContainsKey(attr.Key) && temp[attr.Key].Count > i)
                                temp[attr.Key][i] += attr.Value[i];
                            else
                            {
                                temp[attr.Key].Add(attr.Value[i]);
                            }
                        }
                    }
                }

                return temp;
            }
        }

        public static SkillTree CreateSkillTree(StartLoadingWindow start = null, UpdateLoadingWindow update = null,
            CloseLoadingWindow finish = null)
        {
            string skilltreeobj = "";
            if (Directory.Exists("Data"))
            {
                if (File.Exists("Data\\Skilltree.txt"))
                {
                    skilltreeobj = File.ReadAllText("Data\\Skilltree.txt");
                }
            }
            else
            {
                Directory.CreateDirectory("Data");
                Directory.CreateDirectory("Data\\Assets");
            }

            bool displayProgress = false;
            if (skilltreeobj == "")
            {
                displayProgress = (start != null && update != null && finish != null);
                if (displayProgress)
                    start();
                string uriString = "http://www.pathofexile.com/passive-skill-tree/";
                var req = (HttpWebRequest) WebRequest.Create(uriString);
                var resp = (HttpWebResponse) req.GetResponse();
                string code = new StreamReader(resp.GetResponseStream()).ReadToEnd();
                var regex = new Regex("var passiveSkillTreeData.*");
                skilltreeobj = regex.Match(code).Value.Replace("root", "main").Replace("\\/", "/");
                skilltreeobj = skilltreeobj.Substring(27, skilltreeobj.Length - 27 - 2) + "";
                File.WriteAllText("Data\\Skilltree.txt", skilltreeobj);
            }

            if (displayProgress)
                update(25, 100);
            var skillTree = new SkillTree(skilltreeobj, displayProgress, update);
            if (displayProgress)
                finish();
            return skillTree;
        }

        public void ForceRefundNode(ushort nodeId)
        {
            if (!SkilledNodes.Remove(nodeId))
                throw new InvalidOperationException();

            //SkilledNodes.Remove(nodeId);

            var front = new HashSet<ushort>();
            front.Add(SkilledNodes.First());
            foreach (SkillNode i in Skillnodes[SkilledNodes.First()].Neighbor)
                if (SkilledNodes.Contains(i.id))
                    front.Add(i.id);
            var skilled_reachable = new HashSet<ushort>(front);
            while (front.Count > 0)
            {
                var newFront = new HashSet<ushort>();
                foreach (ushort i in front)
                    foreach (ushort j in Skillnodes[i].Neighbor.Select(nd => nd.id))
                        if (!skilled_reachable.Contains(j) && SkilledNodes.Contains(j))
                        {
                            newFront.Add(j);
                            skilled_reachable.Add(j);
                        }

                front = newFront;
            }

            SkilledNodes = skilled_reachable;
            AvailNodes = new HashSet<ushort>();
            UpdateAvailNodes();
        }

        public HashSet<ushort> ForceRefundNodePreview(ushort nodeId)
        {
            if (!SkilledNodes.Remove(nodeId))
                return new HashSet<ushort>();

            SkilledNodes.Remove(nodeId);

            var front = new HashSet<ushort>();
            front.Add(SkilledNodes.First());
            foreach (SkillNode i in Skillnodes[SkilledNodes.First()].Neighbor)
                if (SkilledNodes.Contains(i.id))
                    front.Add(i.id);

            var skilled_reachable = new HashSet<ushort>(front);
            while (front.Count > 0)
            {
                var newFront = new HashSet<ushort>();
                foreach (ushort i in front)
                    foreach (ushort j in Skillnodes[i].Neighbor.Select(nd => nd.id))
                        if (!skilled_reachable.Contains(j) && SkilledNodes.Contains(j))
                        {
                            newFront.Add(j);
                            skilled_reachable.Add(j);
                        }

                front = newFront;
            }

            var unreachable = new HashSet<ushort>(SkilledNodes);
            foreach (ushort i in skilled_reachable)
                unreachable.Remove(i);
            unreachable.Add(nodeId);

            SkilledNodes.Add(nodeId);

            return unreachable;
        }

        public List<ushort> GetShortestPathTo(ushort targetNode)
        {
            if (SkilledNodes.Contains(targetNode))
                return new List<ushort>();
            if (AvailNodes.Contains(targetNode))
                return new List<ushort> {targetNode};
            var visited = new HashSet<ushort>(SkilledNodes);
            var distance = new Dictionary<int, int>();
            var parent = new Dictionary<ushort, ushort>();
            var newOnes = new Queue<ushort>();
            foreach (ushort node in SkilledNodes)
            {
                distance.Add(node, 0);
            }
            foreach (ushort node in AvailNodes)
            {
                newOnes.Enqueue(node);
                distance.Add(node, 1);
            }
            while (newOnes.Count > 0)
            {
                ushort newNode = newOnes.Dequeue();
                int dis = distance[newNode];
                visited.Add(newNode);
                foreach (ushort connection in Skillnodes[newNode].Neighbor.Select(nd => nd.id))
                {
                    if (visited.Contains(connection))
                        continue;
                    if (distance.ContainsKey(connection))
                        continue;
                    if (Skillnodes[newNode].spc.HasValue)
                        continue;
                    if (Skillnodes[newNode].Mastery)
                        continue;
                    distance.Add(connection, dis + 1);
                    newOnes.Enqueue(connection);

                    parent.Add(connection, newNode);

                    if (connection == targetNode)
                        break;
                }
            }

            if (!distance.ContainsKey(targetNode))
                return new List<ushort>();

            var path = new Stack<ushort>();
            ushort curr = targetNode;
            path.Push(curr);
            while (parent.ContainsKey(curr))
            {
                path.Push(parent[curr]);
                curr = parent[curr];
            }

            var result = new List<ushort>();
            while (path.Count > 0)
                result.Add(path.Pop());

            return result;
        }

        public void HighlightNodes(string search, bool useregex, SolidColorBrush brushColor = null)
        {
            if (search == "")
            {
                DrawHighlights(_highlightnodes = new List<SkillNode>());
                _highlightnodes = null;
                return;
            }

            if (useregex)
            {
                try
                {
                    List<SkillNode> nodes =
                        _highlightnodes =
                            Skillnodes.Values.Where(
                                nd =>
                                    nd.attributes.Where(att => new Regex(search, RegexOptions.IgnoreCase).IsMatch(att))
                                        .Count() > 0 ||
                                    new Regex(search, RegexOptions.IgnoreCase).IsMatch(nd.name) && !nd.Mastery).ToList();
                    DrawHighlights(_highlightnodes, brushColor);
                }
                catch (Exception)
                {
                }
            }
            else
            {
                _highlightnodes =
                    Skillnodes.Values.Where(
                        nd =>
                            nd.attributes.Where(att => att.ToLower().Contains(search.ToLower())).Count() != 0 ||
                            nd.name.ToLower().Contains(search.ToLower()) && !nd.Mastery).ToList();

                DrawHighlights(_highlightnodes, brushColor);
            }
        }

        public Dictionary<string, List<float>> ImplicitAttributes(Dictionary<string, List<float>> attribs)
        {
            var retval = new Dictionary<string, List<float>>();
            // +# to Strength", co["base_str"].Value<int>() }, { "+# to Dexterity", co["base_dex"].Value<int>() }, { "+# to Intelligence", co["base_int"].Value<int>() } };
            retval["+# to maximum Mana"] = new List<float>
            {
                attribs["+# to Intelligence"][0] / IntPerMana + _level * ManaPerLevel
            };
            retval["+#% Energy Shield"] = new List<float> {attribs["+# to Intelligence"][0] / IntPerES};

            retval["+# to maximum Life"] = new List<float>
            {
                attribs["+# to Strength"][0] / IntPerMana + _level * LifePerLevel
            };
            // Every 10 strength grants 2% increased melee physical damage. 
            int str = (int)attribs["+# to Strength"][0];
            if (str % (int)StrPerED > 0) str += (int)StrPerED - (str % (int)StrPerED);
            retval["#% increased Melee Physical Damage"] = new List<float> { str / StrPerED };
            // Every point of Dexterity gives 2 additional base accuracy, and characters gain 2 base accuracy when leveling up.
            // @see http://pathofexile.gamepedia.com/Accuracy
            retval["+# Accuracy Rating"] = new List<float> { attribs["+# to Dexterity"][0] / DexPerAcc + (_level - 1) * AccPerLevel };
            retval["Evasion Rating: #"] = new List<float> {_level * EvasPerLevel};

            // Dexterity value is not getting rounded up any more but rounded normally to the nearest multiple of 5.
            // @see http://pathofexile.gamepedia.com/Talk:Evasion
            float dex = attribs["+# to Dexterity"][0];
            dex = (float)Math.Round(dex / DexPerEvas, 0, MidpointRounding.AwayFromZero) * DexPerEvas;
            retval["#% increased Evasion Rating"] = new List<float> { dex / DexPerEvas };

            return retval;
        }

        public void LoadFromURL(string url)
        {
            string s =
                url.Substring(TreeAddress.Length + (url.StartsWith("https") ? 1 : 0))
                    .Replace("-", "+")
                    .Replace("_", "/");
            byte[] decbuff = Convert.FromBase64String(s);
            int i = BitConverter.ToInt32(new[] {decbuff[3], decbuff[2], decbuff[1], decbuff[1]}, 0);
            byte b = decbuff[4];
            long j = 0L;
            if (i > 0)
                j = decbuff[5];
            var nodes = new List<UInt16>();
            for (int k = 6; k < decbuff.Length; k += 2)
            {
                byte[] dbff = {decbuff[k + 1], decbuff[k + 0]};
                if (Skillnodes.Keys.Contains(BitConverter.ToUInt16(dbff, 0)))
                    nodes.Add((BitConverter.ToUInt16(dbff, 0)));
            }
            Chartype = b;
            SkilledNodes.Clear();
            SkillNode startnode = Skillnodes.First(nd => nd.Value.name.ToUpper() == CharName[Chartype].ToUpper()).Value;
            SkilledNodes.Add(startnode.id);
            foreach (ushort node in nodes)
            {
                SkilledNodes.Add(node);
            }
            UpdateAvailNodes();
        }

        public void Reset()
        {
            SkilledNodes.Clear();
            KeyValuePair<ushort, SkillNode> node = Skillnodes.First(nd => nd.Value.name.ToUpper() == CharName[_chartype]);
            SkilledNodes.Add(node.Value.id);
            UpdateAvailNodes();
        }

        public string SaveToURL()
        {
            var b = new byte[(SkilledNodes.Count - 1) * 2 + 6];
            byte[] b2 = BitConverter.GetBytes(2);
            b[0] = b2[3];
            b[1] = b2[2];
            b[2] = b2[1];
            b[3] = b2[0];
            b[4] = (byte) (Chartype);
            b[5] = 0;
            int pos = 6;
            foreach (ushort inn in SkilledNodes)
            {
                if (CharName.Contains(Skillnodes[inn].name.ToUpper()))
                    continue;
                byte[] dbff = BitConverter.GetBytes((Int16) inn);
                b[pos++] = dbff[1];
                b[pos++] = dbff[0];
            }
            return TreeAddress + Convert.ToBase64String(b).Replace("/", "_").Replace("+", "-");
        }

        public void SkillAllHighligtedNodes()
        {
            if (_highlightnodes == null)
                return;
            var nodes = new HashSet<int>();
            foreach (SkillNode nd in _highlightnodes)
            {
                nodes.Add(nd.id);
            }
            SkillStep(nodes);
        }

        private HashSet<int> SkillStep(HashSet<int> hs)
        {
            var pathes = new List<List<ushort>>();
            foreach (SkillNode nd in _highlightnodes)
            {
                pathes.Add(GetShortestPathTo(nd.id));
            }
            pathes.Sort((p1, p2) => p1.Count.CompareTo(p2.Count));
            pathes.RemoveAll(p => p.Count == 0);
            foreach (ushort i in pathes[0])
            {
                hs.Remove(i);
                SkilledNodes.Add(i);
            }
            UpdateAvailNodes();

            return hs.Count == 0 ? hs : SkillStep(hs);
        }

        public void UpdateAvailNodes()
        {
            AvailNodes.Clear();
            foreach (ushort inode in SkilledNodes)
            {
                SkillNode node = Skillnodes[inode];
                foreach (SkillNode skillNode in node.Neighbor)
                {
                    if (!CharName.Contains(skillNode.name) && !SkilledNodes.Contains(skillNode.id))
                        AvailNodes.Add(skillNode.id);
                }
            }
            //  picActiveLinks = new DrawingVisual();

            var pen2 = new Pen(Brushes.Yellow, 15f);

            using (DrawingContext dc = picActiveLinks.RenderOpen())
            {
                foreach (ushort n1 in SkilledNodes)
                {
                    foreach (SkillNode n2 in Skillnodes[n1].Neighbor)
                    {
                        if (SkilledNodes.Contains(n2.id))
                        {
                            DrawConnection(dc, pen2, n2, Skillnodes[n1]);
                        }
                    }
                }
            }
            // picActiveLinks.Clear();
            DrawNodeSurround();
        }
    }
}