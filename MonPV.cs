using Life;
using UnityEngine;
using MyMenu.Entities;
using UIPanelManager;
using Life.UI;
using System;
using Life.Network;
using Life.BizSystem;
using Life.VehicleSystem;
using Mirror;
using Life.PermissionSystem;
using System.Collections.Generic;
using System.IO;
using Config.config;
using Newtonsoft.Json;
using FoxORM;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Security.Claims;
using Life.DB;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Xml.Linq;

namespace MonPV
{
    public class MonPV : Plugin
    {
        Dictionary<uint, long> Temps = new Dictionary<uint, long>();
        private LifeServer server;
        private int _prixMinimum;
        private int _prixMaximum;
        private int _temps;
        private string _webhook;
        private string _smsNom;
        private string _titreNotification;
        private string _messageNotification;
        private string _titreNotif;
        private string _messageNotif;
        private FoxOrm _foxOrm;

        public MonPV(IGameAPI aPI) : base(aPI)
        {

        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();
            Debug.Log("MonPV  a été initialisé avec succès!");
            Section section = new Section(Section.GetSourceName(), Section.GetSourceName(), "v1.0.0", "French Aero");
            Action<UIPanel> action = ui => MaFonction(section.GetPlayer(ui));
            section.SetBizIdAllowed();
            section.SetBizTypeAllowed(Activity.Type.LawEnforcement);
            section.OnlyAdmin = false;
            section.MinAdminLevel = 0;
            section.Line = new UITabLine(section.Title, action);
            section.Insert(false);
            Section section2 = new Section("MesAmendes", "MesAmendes", "v1.0.0", "French Aero");
            Action<UIPanel> action2 = ui3 => MaFonction2Async(section.GetPlayer(ui3));
            section2.OnlyAdmin = false;
            section2.MinAdminLevel = 0;
            section2.Line = new UITabLine(section2.Title, action2);
            section2.Insert(false);

            var configFilePath = Path.Combine(pluginsPath, "MonPV/config.json");
            var globalConfiguration = ChargerConfiguration(configFilePath);
            _prixMinimum = globalConfiguration.prixMinimum;
            _prixMaximum = globalConfiguration.prixMaximum;
            _temps = globalConfiguration.temps;
            _webhook = globalConfiguration.Webhook;
            _titreNotif = globalConfiguration.titreNotif;
            _smsNom = globalConfiguration.smsNom;
            _messageNotif = globalConfiguration.messageNotif;

            _foxOrm = new FoxOrm(pluginsPath + "/MonPV/database.sqlite");
            _foxOrm.RegisterTable<Historique>();
        }

        public void MaFonction(Player player)
        {
            UIPanel panel = new UIPanel("Contravention", UIPanel.PanelType.Input);
            panel.SetTitle("Plaque d'immatriculation");
            panel.inputPlaceholder = "Numéro de plaque d'immatriculation";
            panel.AddButton("Fermer", (Action<UIPanel>)(ui => player.ClosePanel(ui)));
            panel.AddButton("Rechercher", (Action<UIPanel>)(async ui =>
            {
                string inputText = ui.inputText;
                LifeVehicle vehicle = Nova.v.GetVehicle(inputText);
                if (vehicle != null)
                {
                    Player player1 = Nova.server.GetPlayer(vehicle.permissions.owner.characterId);
                    if (player1 != null)
                    {
                        player.ClosePanel(panel);
                        UIPanel panel2 = new UIPanel("Contravention", UIPanel.PanelType.Input);
                        panel2.SetTitle("Montant de l'amende");
                        panel2.inputPlaceholder = "Prix de l'amende";
                        panel2.AddButton("Fermer", (Action<UIPanel>)(ui2 => player.ClosePanel(ui2)));
                        panel2.AddButton("Valider", (Action<UIPanel>)(async ui2 =>
                        {
                            var prixAmende = double.Parse(panel2.inputText);
                            if (prixAmende > _prixMaximum)
                            {
                                PanelManager.Notification(player, "PV", "Vous ne pouvez pas mettre un PV supérieur à " + _prixMaximum + " euros.", NotificationManager.Type.Info);
                            }
                            else if (prixAmende < _prixMinimum)
                            {
                                PanelManager.Notification(player, "PV", "Vous ne pouvez pas mettre un PV inférieur à " + _prixMinimum + " euros.", NotificationManager.Type.Info);
                            }
                            else
                            {
                                DateTime Temps2 = DateTime.Now;
                                if (Temps.ContainsKey(player.netId))
                                {
                                    if (Temps2.Ticks - Temps[player.netId] > TimeSpan.FromMinutes(_temps).Ticks)
                                    {
                                        var message = "Vous avez été verbalisé d'un montant de " + prixAmende + " euros.";
                                        Temps[player.netId] = Temps2.Ticks;
                                        PanelManager.Notification(player, "PV", "Nous avons envoyé le PV à : " + player1.GetFullName() + ".", NotificationManager.Type.Success);
                                        player1.AddBankMoney(-1 * prixAmende);
                                        var historique = new Historique { character = player1.character.Id, historique = DateTime.Now, plaque = inputText, prix = panel2.inputText };
                                        bool result = await _foxOrm.Save<Historique>(historique);
                                        SendSMS(message, player1.character.Id, player1.character.PhoneNumber);
                                        SendNotification(player1);
                                        SendWebhook(_webhook, "L'agent " + player.GetFullName() + " a mit une contravention à la plaque: " + inputText + " au nom de: " + player1.GetFullName() + " à " + panel2.inputText + " euros.");
                                    }
                                    else
                                    {
                                        PanelManager.Notification(player, "PV", "Vous ne pouvez pas mettre de PV plus de toutes les " + _temps + " minutes", NotificationManager.Type.Info);
                                    }
                                }
                                else
                                {
                                    var message = "Vous avez été verbalisé d'un montant de " + prixAmende + " euros.";
                                    PanelManager.Notification(player, "PV", "Nous avons envoyé le PV à : " + player1.GetFullName() + ".", NotificationManager.Type.Success);
                                    player1.AddBankMoney(-1 * prixAmende);
                                    var historique = new Historique { character = player1.character.Id, historique = DateTime.Now, plaque = inputText, prix = panel2.inputText };
                                    bool result = await _foxOrm.Save<Historique>(historique);
                                    SendWebhook(_webhook, "L'agent " + player.GetFullName() + " a mit une contravention à la plaque: " + inputText + " au nom de: " + player1.GetFullName() + " à " + panel2.inputText + " euros.");
                                    SendSMS(message, player1.character.Id, player1.character.PhoneNumber);
                                    SendNotification(player1);
                                    Temps.Add(player.netId, Temps2.Ticks);
                                }
                                player.ClosePanel(panel2);
                            };
                        }));
                        player.ShowPanelUI(panel2);
                    }
                    else
                    {
                        var player2 = await LifeDB.FetchCharacter(vehicle.permissions.owner.characterId);
                        player.ClosePanel(panel);
                        UIPanel panel4 = new UIPanel("Contravention", UIPanel.PanelType.Input);
                        panel4.SetTitle("Montant de l'amende");
                        panel4.inputPlaceholder = "Prix de l'amende";
                        panel4.AddButton("Fermer", (Action<UIPanel>)(ui2 => player.ClosePanel(ui2)));
                        panel4.AddButton("Valider", (Action<UIPanel>)(async ui2 =>
                        {
                            var prixAmende = double.Parse(panel4.inputText);
                            if (prixAmende > _prixMaximum)
                            {
                                PanelManager.Notification(player, "PV", "Vous ne pouvez pas mettre un PV supérieur à " + _prixMaximum + " euros.", NotificationManager.Type.Info);
                            }
                            else if (prixAmende < _prixMinimum)
                            {
                                PanelManager.Notification(player, "PV", "Vous ne pouvez pas mettre un PV inférieur à " + _prixMinimum + " euros.", NotificationManager.Type.Info);
                            }
                            else
                            {
                                DateTime Temps2 = DateTime.Now;
                                if (Temps.ContainsKey(player.netId))
                                {
                                    if (Temps2.Ticks - Temps[player.netId] > TimeSpan.FromMinutes(_temps).Ticks)
                                    {
                                        var message = "Vous avez été verbalisé d'un montant de " + prixAmende + " euros.";
                                        Temps[player.netId] = Temps2.Ticks;
                                        PanelManager.Notification(player, "PV", "Nous avons envoyé le PV à : " + player2.Firstname + player2.Lastname + ".", NotificationManager.Type.Success);
                                        player2.Bank -= double.Parse(panel4.inputText);
                                        player2.Save();
                                        var historique = new Historique { character = player2.Id, historique = DateTime.Now, plaque = inputText, prix = panel4.inputText };
                                        bool result = await _foxOrm.Save<Historique>(historique);
                                        SendSMS(message, player2.Id, player2.PhoneNumber);
                                        SendWebhook(_webhook, "L'agent " + player.GetFullName() + " a mit une contravention à la plaque: " + inputText + " au nom de: " + player2.Firstname + player2.Lastname + " à " + panel4.inputText + " euros.");

                                    }
                                    else
                                    {
                                        PanelManager.Notification(player, "PV", "Vous ne pouvez pas mettre de PV plus de toutes les " + _temps + " minutes", NotificationManager.Type.Info);
                                    }
                                }
                                else
                                {
                                    var message = "Vous avez été verbalisé d'un montant de " + prixAmende + " euros.";
                                    PanelManager.Notification(player, "PV", "Nous avons envoyé le PV à : " + player2.Firstname + player2.Lastname + ".", NotificationManager.Type.Success);
                                    player2.Bank -= double.Parse(panel4.inputText);
                                    var historique = new Historique { character = player2.Id, historique = DateTime.Now, plaque = inputText, prix = panel4.inputText };
                                    bool result = await _foxOrm.Save<Historique>(historique);
                                    SendWebhook(_webhook, "L'agent " + player.GetFullName() + " a mit une contravention à la plaque: " + inputText + " au nom de: " + player2.Firstname + player2.Lastname + " à " + panel4.inputText + " euros.");
                                    SendSMS(message, player2.Id, player2.PhoneNumber);
                                    Temps.Add(player.netId, Temps2.Ticks);
                                }
                                player.ClosePanel(panel4);
                            };
                        }));
                        player.ShowPanelUI(panel4);
                    }

                }
                else
                    PanelManager.Notification(player, "PV", "Aucun véhicule avec cette plaque d'immatriculation n'a été trouvé!", NotificationManager.Type.Error);
            }));
            player.ShowPanelUI(panel);

         
        }

        public async Task MaFonction2Async(Player player)
        {
            UIPanel panel3 = new UIPanel("Historique des contraventions", UIPanel.PanelType.Tab);
            panel3.SetTitle("Historique des contraventions");
            panel3.AddButton("Fermer", (Action<UIPanel>)(ui3 => player.ClosePanel(ui3)));
            var queriedHistorique = await _foxOrm.Query<Historique>(historique => historique.character == player.character.Id);
            foreach (Historique historique in queriedHistorique)
            {
                panel3.AddTabLine("Plaque: " + historique.plaque + " Prix: " + historique.prix, null); 
            }
            player.ShowPanelUI(panel3);
        }


        private static MainConfig ChargerConfiguration(string configFilePath)
        {
            if (!Directory.Exists(Path.GetDirectoryName(configFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));
            }
            if (!File.Exists(configFilePath))
            {
                File.WriteAllText(configFilePath, "{\n  \"prixMinimum\": 10,\n  \"prixMaximum\": 135,\n  \"temps\": 30,\n  \"Webhook\": null,\n  \"smsNom\": null,\n  \"titreNotif\": null,\n  \"messageNotif\": null\n}");
            }
            var jsonConfig = File.ReadAllText(configFilePath);
            return JsonConvert.DeserializeObject<MainConfig>(jsonConfig);
        }

        private static async Task SendWebhook(string webhookUrl, string content)
        {
            using (var client = new HttpClient())
            {
                var payload = new
                {
                    content = content
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);

                var data = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(webhookUrl, data);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Erreur lors de l'envoi du webhook. Statut : {response.StatusCode}");
                }
            }
        }

        public async void SendSMS(string message, int playerId, string playerPhoneNumber)
        {
            await LifeDB.SendSMS(playerId, "17", playerPhoneNumber, Nova.UnixTimeNow(),
            message);
                var contacts = await LifeDB.FetchContacts(playerId);
                var contactPub = contacts.contacts.Where(contact => contact.number == "17").ToList();
                if (contactPub.Count == 0)
                {
                    await LifeDB.CreateContact(playerId, "17", _smsNom);
                }
        }

        public void SendNotification(Player player)
        {
            player.setup.TargetUpdateSMS();
            player.Notify(_titreNotif, _messageNotif);
        }

    }
}
