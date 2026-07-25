using System;
using System.Collections.Generic;

namespace NgSharp.Benchmark.Realistic;

// Anonymized data models + generators for the "realistic document" benchmark. Shapes mirror the structural
// complexity of the real ProxAffiche print/PDF templates (devis / fiche-produit / listes-cartes) — nested
// sections, many line items, per-line conditionals and number/date formatting — with entirely neutral
// content (Acme Média, generic campaigns/cities/prices). No business data.
public static class RealisticData
{
    private static readonly string[] Cities =
    {
        "Lyon", "Marseille", "Bordeaux", "Lille", "Nantes", "Toulouse",
        "Strasbourg", "Rennes", "Montpellier", "Nice", "Grenoble", "Rouen",
    };

    private static readonly string[] Formats = { "Grand format", "Abribus", "Écran digital", "Mobilier urbain" };

    private static readonly string[] Statuses = { "Disponible", "Réservé", "En option", "Complet" };

    public static Quote BuildQuote()
    {
        var issue = new DateTime(2024, 3, 14);
        var sections = new List<QuoteSection>();
        var totalHt = 0m;
        var totalDiscount = 0m;
        var totalImpr = 0;

        for (var s = 0; s < 6; s++)
        {
            var lines = new List<QuoteLine>();
            var subtotal = 0m;
            var sectionImpr = 0;

            for (var l = 0; l < 12; l++)
            {
                var qty = 3 + (l % 5);
                var unit = 120 + (s * 40) + (l * 15);
                var discount = (l % 4 == 0) ? unit * qty * 0.10m : 0m;
                var lineHt = (unit * qty) - discount;
                var impressions = 45000 + (s * 8000) + (l * 2500);

                lines.Add(new QuoteLine
                {
                    Ref = $"EMP-{s + 1:D2}-{l + 1:D3}",
                    Label = $"Emplacement {Cities[(s + l) % Cities.Length]} #{l + 1}",
                    Format = Formats[l % Formats.Length],
                    City = Cities[(s + l) % Cities.Length],
                    Quantity = qty,
                    Impressions = impressions,
                    UnitPrice = unit,
                    Discount = discount,
                    TotalHT = lineHt,
                    StartDate = issue.AddDays(21 + s * 7),
                    EndDate = issue.AddDays(21 + s * 7 + 13),
                    InStock = (l % 3) != 0,
                    OnOption = (l % 5) == 0,
                    Highlighted = (l % 6) == 0,
                });

                subtotal += lineHt;
                sectionImpr += impressions;
            }

            totalHt += subtotal;
            totalDiscount += 0;
            totalImpr += sectionImpr;

            sections.Add(new QuoteSection
            {
                Title = $"Réseau {Cities[s]}",
                Subtitle = $"Zone {s + 1} — {Formats[s % Formats.Length]}",
                HasDiscount = (s % 2) == 0,
                SubtotalHT = subtotal,
                SectionImpressions = sectionImpr,
                Lines = lines,
            });
        }

        var tvaRate = 0.20m;
        var tva = Math.Round(totalHt * tvaRate, 2);

        return new Quote
        {
            Number = "DEV-2024-004217",
            Reference = "REF-CAMP-88213",
            CampaignName = "Campagne Printemps Multi-Réseaux",
            IssueDate = issue,
            ValidUntil = issue.AddDays(30),
            CampaignStart = issue.AddDays(21),
            CampaignEnd = issue.AddDays(90),
            Issuer = new Party
            {
                Name = "Acme Média SAS",
                Address = "12 rue des Exemples",
                Zip = "75002",
                City = "Paris",
                Siret = "12345678900021",
                Tva = "FR00123456789",
                Phone = "01 23 45 67 89",
                Email = "contact@acme-media.example",
                Contact = "Service Commercial",
            },
            Client = new Party
            {
                Name = "Client Démonstration SARL",
                Address = "8 avenue du Test",
                Zip = "69003",
                City = "Lyon",
                Contact = "Responsable Achats",
                Phone = "04 00 00 00 00",
                Email = "achats@client-demo.example",
            },
            Sections = sections,
            TotalImpressions = totalImpr,
            TotalHT = totalHt,
            TotalDiscount = totalDiscount,
            TvaRate = tvaRate,
            TotalTva = tva,
            TotalTTC = totalHt + tva,
            HasOptions = true,
            Options = new List<TextItem>
            {
                new() { Text = "Habillage créatif sur mesure" },
                new() { Text = "Reporting de diffusion hebdomadaire" },
                new() { Text = "Géolocalisation renforcée" },
            },
            Terms = new List<TextItem>
            {
                new() { Text = "Devis valable 30 jours à compter de la date d'émission." },
                new() { Text = "Diffusion sous réserve de disponibilité des emplacements." },
                new() { Text = "Tout report de campagne doit être signalé 10 jours avant le début." },
                new() { Text = "Les prix s'entendent hors taxes, TVA 20% applicable." },
            },
            Notes = "Merci de votre confiance. Ce document est un exemple anonymisé.",
        };
    }

    public static ProductSheet BuildProductSheet()
    {
        var updated = new DateTime(2024, 3, 14);

        var specs = new List<Spec>();

        for (var i = 0; i < 14; i++)
        {
            specs.Add(new Spec
            {
                Label = $"Caractéristique {i + 1}",
                Value = $"Valeur {Cities[i % Cities.Length]} {i + 1}",
                Highlighted = (i % 3) == 0,
            });
        }

        var slots = new List<Slot>();

        for (var i = 0; i < 10; i++)
        {
            slots.Add(new Slot
            {
                Period = $"Semaine {i + 11}",
                StartDate = updated.AddDays(i * 7),
                EndDate = updated.AddDays(i * 7 + 6),
                Price = 480 + (i * 65),
                Available = (i % 3) != 1,
                LastMinute = (i % 4) == 0,
            });
        }

        return new ProductSheet
        {
            Ref = "EMP-DOOH-77219",
            Name = "Emplacement Panoramique Centre-Ville",
            Format = "Écran digital grand format",
            City = "Lyon",
            Address = "Place de l'Exemple, 69002 Lyon",
            UpdatedAt = updated,
            IsDigital = true,
            HasLighting = true,
            HasAudienceData = true,
            IsPremium = true,
            NearTransport = true,
            Available = true,
            Impressions = 1_850_000,
            Reach = 420_000,
            Frequency = 4.4m,
            BasePrice = 6200,
            PremiumSurcharge = 1200,
            Specs = specs,
            Slots = slots,
            Description = "Emplacement de démonstration anonymisé — données neutres à des fins de benchmark.",
        };
    }

    public static CardList BuildCardList()
    {
        var generated = new DateTime(2024, 3, 14);
        var cards = new List<Card>();
        var totalImpr = 0;
        var available = 0;

        for (var i = 0; i < 40; i++)
        {
            var impressions = 32000 + (i * 4100);
            var avail = (i % 3) != 0;

            cards.Add(new Card
            {
                Ref = $"CARD-{i + 1:D4}",
                Name = $"Emplacement {Cities[i % Cities.Length]} #{i + 1}",
                City = Cities[i % Cities.Length],
                Format = Formats[i % Formats.Length],
                Status = Statuses[i % Statuses.Length],
                Price = 150 + (i * 22),
                Impressions = impressions,
                NextSlot = generated.AddDays(3 + (i % 21)),
                Available = avail,
                Digital = (i % 2) == 0,
                Promo = (i % 5) == 0,
            });

            totalImpr += impressions;

            if (avail)
            {
                available++;
            }
        }

        return new CardList
        {
            Title = "Inventaire Réseau — Extrait",
            Region = "Multi-Régions",
            GeneratedAt = generated,
            TotalImpressions = totalImpr,
            AvailableCount = available,
            Cards = cards,
        };
    }
}
