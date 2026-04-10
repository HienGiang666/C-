# Project Structure Lite

## Scope
Only inspect `TourApp.Mobile` first.
Do not inspect API/CMS unless later requested.

## Goal
Clean duplicate pages and startup flow.
Keep build stable.
MapPage is the center of the mobile app.

## Keep
- App.xaml
- App.xaml.cs
- AppShell.xaml
- AppShell.xaml.cs
- MauiProgram.cs
- Services/*
- Views/Auth/*
- Views/HomePage*
- Views/POIPage*
- Views/MapPage*
- Views/TourPage*
- Views/ProfilePage*
- Views/QRScannerPage*

## Review / likely duplicate
- Views/LoginPage*
- Views/RegisterPage*
- MainPage*

## Target mobile flow
App start
-> if not logged in: Views/Auth/LoginPage
-> if logged in: AppShell

AppShell tabs
- HomePage
- POIPage
- MapPage
- TourPage
- ProfilePage

## Navigation target
- HomePage -> MapPage
- POIPage -> MapPage(poiId)
- TourPage -> MapPage(tourId)

## Rule
Do not rewrite the whole project.
Do not delete files unless clearly duplicate or unused.
Do not edit code in analysis step.