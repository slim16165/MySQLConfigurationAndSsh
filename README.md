# MySQLConfigurationAndSsh

[![NuGet](https://img.shields.io/badge/NuGet-v1.0.0-blue.svg)](#) <!-- Esempio di badge NuGet (link da aggiornare) -->
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](#) <!-- Esempio di badge CI/CD -->
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](#) <!-- Esempio di badge Licenza -->
[![Last Commit](https://img.shields.io/github/last-commit/username/repo)](#) <!-- Esempio di badge ultimi commit -->

> **Scopo**: Questa libreria fornisce un set di classi per configurare e utilizzare MySQL in .NET (Framework o .NET Core) con supporto nativo al tunneling SSH.  
> **Target**: Sviluppatori .NET che necessitano di connettersi a database MySQL remoti, spesso su hosting condivisi o VPS, dove un tunnel SSH è consigliato (o obbligatorio).

## Indice

1. [Caratteristiche Principali](#caratteristiche-principali)
2. [Use Case & Benefici](#use-case--benefici)
3. [Installazione](#installazione)
4. [Esempio di Utilizzo](#esempio-di-utilizzo)
5. [Struttura dei File Principali](#struttura-dei-file-principali)
6. [Demo Rapida](#demo-rapida)
7. [Contributi e Community](#contributi-e-community)
8. [Licenza](#licenza)

---

## Caratteristiche Principali
- **Configurazione centralizzata**: Carica e salva le credenziali (sia DB che SSH) da file XML tramite `GenericMySQLConfigurationNew`.
- **SSH Tunneling on-demand**: Attivazione automatica di un port forwarding locale, con gestione dei conflitti di porta.
- **DAL MySQL semplice**: `MySqlDal` e `MySqlDalAsync` forniscono metodi pronti all’uso per query, bulk copy, scalar e reader asincrono/sincrono.
- **Utility di sistema**: Scoperta IP pubblico, IP locale in uscita, e analisi porte in uso su Windows (via `netstat`).
- **Parser e helper**: Conversione robusta e tipizzata di date e boolean, estrazione pattern, e log con `NLog`.
- **Flessibile & modulare**: Le classi sono pensate per essere estese o sovrascritte, facilitando l’adattamento a progetti specifici.

---

## Use Case & Benefici

- **Hosting condiviso**: quando è possibile accedere a MySQL solo tramite SSH, la libreria crea il tunnel e reindirizza le connessioni MySQL in modo trasparente.
- **Integrazione con applicazioni desktop**: se la porta predefinita è occupata, è possibile segnalare all’utente e offrire azioni (es. kill processi).  
- **Monitoraggio e manutenzione database**: script di manutenzione che necessitano di connettersi a più siti, caricando da un unico file `.config` un elenco di siti/credenziali.
- **Multi-ambiente**: passare velocemente da un database di test in locale a uno di produzione remoto (basta cambiare la `SelectedWebsite`).

**Pain point risolti**:
- Configurazione SSH e gestione delle credenziali su più ambienti.
- Errore di porta già in uso (3306) e conseguente noiosa ricerca del processo che blocca la porta.
- Logica ripetitiva di connessione MySQL (aprire, chiudere, catturare errori, gestire time-out).
- Necessità di un approccio batch veloce (BulkCopy) in contesti ETL.

**Vantaggi principali**:
- **Riduce la complessità** nel gestire SSH da codice .NET.
- **Semplifica la gestione** delle connessioni e risparmia tempo in debug.
- **Elevata trasparenza**: la configurazione è in XML, facilmente versionabile e modificabile.
- **Estendibilità**: puoi personalizzare classi e metodi per scenari specifici.

---

## Analisi di alto livello

La *MySQLConfigurationAndSsh* è una libreria .NET che semplifica la configurazione e la gestione di connessioni MySQL, con la possibilità di creare tunnel SSH in modo sicuro e trasparente. Il cuore del progetto è la gestione centralizzata delle impostazioni (host, porta, credenziali DB/SSH) e l’abilitazione automatica di un port forwarding SSH qualora necessario.

Ecco gli aspetti principali:

- **Connessione MySQL semplificata**: tramite classi come `MySqlConnectionAppConfig` e `WebsiteAppConfigBase`, si definiscono in modo dichiarativo host, utente, password e database.
- **SSH Tunneling integrato**: la classe `ConnectionHelper` abilita il tunnel SSH quando serve, controlla se la porta MySQL (tipicamente 3306) è già in uso e, se necessario, gestisce anche la terminazione dei processi in conflitto.
- **Caricamento/Saving configurazioni**: con `GenericMySQLConfigurationNew` si salvano e caricano le impostazioni da file XML (`Sites.config`), impostando un *SelectedWebsite* che determina le credenziali da usare.
- **Utility di rete e diagnostica**: la libreria fornisce strumenti per individuare l’IP pubblico, l’IP locale usato in uscita e per analizzare porte in uso mediante `netstat` (classi `ProcessPort` e `ProcessPorts`).
- **Data Access Layer (DAL)**: `MySqlDal` e `MySqlDalAsync` agevolano query, stored procedure, bulk copy e gestione asincrona di letture/scritture.
- **Parser generici**: con `DateParserHelper` e `ParsedUnparsed<T>` è possibile estrarre e convertire in modo sicuro dati grezzi (es. stringhe da file/Excel) in tipi forti (DateTime, bool, ecc.), gestendo eventuali errori di parsing.
- **Integrazione UI**: `UIHelper` e `TupleEventArgs` offrono eventi e metodi pensati per mostrare finestre di dialogo (WPF/WinForms) e chiedere all’utente l’eventuale kill di processi in conflitto.

Il codice è pensato per scenari reali in cui occorre connettersi a un database MySQL remoto (ad esempio hosting di siti WordPress) via SSH, garantendo sicurezza e semplicità. 

---

## Installazione

1. **Pacchetto NuGet** (consigliato):  
   ```bash
   dotnet add package MySQLConfigurationAndSsh
   ```
   (In futuro potrai sostituire con il link/nome reale del pacchetto NuGet.)

2. **Clonare il repository** e includere i file `.cs` nel tuo progetto.  
   ```bash
   git clone https://github.com/tuoUtente/MySQLConfigurationAndSsh.git
   ```

3. **Dependencies**:
   - [.NET 6.0+](https://dotnet.microsoft.com/download) (o .NET Framework 4.6.1+)
   - [MySqlConnector](https://github.com/mysql-net/MySqlConnector) (già referenziato nel progetto)
   - [Renci.SshNet](https://github.com/sshnet/SSH.NET) per il tunnel SSH
   - [NLog](https://nlog-project.org/) per il logging (opzionale ma consigliato)

---

## Esempio di Utilizzo

1. **Carica la configurazione da `Sites.config`:**
   ```csharp
   // Istanzia la tua classe derivata, che implementa la logica personalizzata
   var myConfig = new MyDerivedMySQLConfiguration();
   GenericMySQLConfigurationNew.Instance = myConfig;
   myConfig.LoadConfig();

   // Imposta il sito desiderato
   myConfig.SelectedWebsiteName = "MioSitoWordPress";
   ```
2. **Avvia la connessione SSH (opzionale)**:
   ```csharp
   using MySQLConfigurationAndSsh;

   // Se la porta 3306 è già in uso, la libreria lo rileva 
   // e può chiedere all'utente di killare il processo in conflitto
   ConnectionHelper.EnableSshIfPossible(3306);
   ```
3. **Esegui query MySQL**:
   ```csharp
   var result = MySqlDal.ExecuteQuery(
       MySqlDal.ConnSitoWp, 
       "SELECT * FROM wp_posts WHERE post_status='publish';"
   );

   // Oppure asincrono
   // var reader = await MySqlDal.ExecuteReaderAsync(cmd);
   ```
4. **Chiudi la connessione**:
   ```csharp
   // Di norma MySqlDal chiude la connessione internamente 
   // dopo l’esecuzione di query. Tuttavia, 
   // se usi un DataReader, ricordati di Dispose/Close manualmente.
   ```

---

## Struttura dei File Principali

- **`MySqlConnectionAppConfig.cs`**: Configurazione di base per host, porta, user, password.  
- **`GenericMySQLConfigurationNew.cs`**: Caricamento/salvataggio XML e gestione del sito selezionato.  
- **`ConnectionHelper.cs`**: Creazione tunnel SSH, check porta 3306, IP utility.  
- **`MySqlDal.cs`** / **`MySqlDalAsync.cs`**: Metodi di utilità per query, non-query, bulk copy, asincrone e sincrone.  
- **`ProcessPorts.cs`** e **`ProcessPort.cs`**: Analisi porte occupate (via netstat) e identificazione processi.  
- **`ParsedUnparsed.cs`** e **`DateParserHelper.cs`**: Parser generici, estrazione date e conversione robusta di stringhe.  
- **`UIHelper.cs`**: Interfacce di supporto con finestre di dialogo (MessageBox in WPF/WinForms).  
- **`TupleEventArgs.cs`**: Helper per creare `EventArgs` contenenti tuple di parametri.

---

## Demo Rapida

In questa gif dimostrativa (o screenshot) puoi vedere come l’app WPF rilevi la porta occupata, chieda all’utente di chiudere il processo e poi stabilisca la connessione SSH.  
*(Inserisci eventuale immagine o GIF se disponibile.)*

---

## Contributi e Community

Contribuire è benvenuto! Apri pure una [issue](#) per discutere miglioramenti, problemi o nuove idee. Per proporre modifiche, fai una pull request sulla branch `dev` con una breve descrizione del contributo.

- **Linee guida**:  
  - Preferibile formattare il codice con gli standard `.editorconfig` del progetto.  
  - Scrivere test o esempi se aggiungi funzionalità importanti.
  - Tenere la documentazione in [Wiki](#) aggiornata.

Se hai dubbi, apri una discussione nella sezione [Discussions](#) o contattaci su [Slack/Discord](#) (link da aggiungere).

---

## Licenza

Progetto rilasciato con [Licenza MIT](./LICENSE).  
Puoi usarlo liberamente in progetti commerciali e non, con l’obbligo di mantenere i riferimenti alla licenza originale.

---

# Change List (Sintesi dei principali aggiornamenti)

Ecco una panoramica *molto sintetica* dei cambiamenti nel tempo, con relative milestone:

- **Novembre 2022**  
  - Aggiunti i file iniziali di configurazione (`MySqlConnectionAppConfig`, `SshCredentials`, `WebsiteAppConfigBase`) e la struttura base in `GenericMySQLConfigurationNew`.
- **Dicembre 2022**  
  - Implementata la logica di tunneling in `ConnectionHelper` (forward su porta 3306, kill processi in conflitto), con evento `ShowMessageBoxEvent`.
- **Gennaio 2023**  
  - Creato il DAL `MySqlDal` e `MySqlDalAsync` per semplificare query, BulkCopy, e operazioni sincrone/asincrone su MySQL.
- **Febbraio 2023**  
  - Inseriti tool di parsing (`DateParserHelper`, `ParsedUnparsed<T>`) per conversioni robuste e logging errori.
- **Marzo 2023**  
  - Sviluppate le classi `ProcessPort` e `ProcessPorts` che sfruttano `netstat` per identificare PID e porte aperte.
- **Aprile 2023**  
  - Aggiunti `TupleEventArgs` e `UIHelper` per la gestione di messaggi UI (MessageBox) e la reazione immediata agli eventi di conflitto di porta.

*(Le date sono indicative e rappresentano una sintesi )*
