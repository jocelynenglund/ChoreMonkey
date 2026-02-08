

# Chore Monkey Rebranding Plan

## Overview
Transform the app from "ChoreHub" (sage green, cozy modern) to "Chore Monkey" with a playful, energetic brand featuring warm jungle tones and monkey-themed elements.

## Brand Identity Changes

### New Color Palette
- **Primary**: Warm brown (`30 50% 35%`) - like monkey fur
- **Accent**: Banana yellow (`45 95% 55%`) - playful highlight
- **Secondary**: Jungle green (`120 30% 40%`) - natural backdrop
- **Background**: Warm cream/tan (`35 40% 96%`) - soft, inviting

### Typography
- Keep Nunito but emphasize the bolder weights for headers
- More playful, rounded feel matches the fun brand

## Files to Update

### 1. src/index.css - Design System Overhaul
- Update CSS variables with monkey-themed colors:
  - Primary: warm brown tones
  - Accent: banana yellow
  - Success: jungle green
- Update gradients to warm brown/yellow tones
- Rename `gradient-sage` to `gradient-monkey`

### 2. tailwind.config.ts
- Update the gradient class reference from `gradient-sage` to `gradient-monkey`

### 3. index.html - Meta Tags
- Title: "Chore Monkey - Family Chore Management"
- Description: "Make chores fun with Chore Monkey! Simple household task management for families."
- Update Open Graph tags

### 4. src/pages/Index.tsx - Landing Page
- Replace Home icon with a monkey emoji or custom SVG
- Update title: `Chore<span>Monkey</span>` with banana yellow accent
- Update tagline: "Make chores fun! Simple task management for busy families."
- Update feature pills text to be more playful
- Update footer: "Made with bananas for families everywhere"

### 5. src/pages/CreateHousehold.tsx
- Update icon styling to use new gradient
- Keep functionality identical

### 6. src/pages/JoinHousehold.tsx
- Update icon styling to use new gradient
- Keep functionality identical

### 7. src/pages/HouseholdDashboard.tsx
- Replace Home icon in header with monkey theme
- Update `gradient-sage` to `gradient-monkey`

### 8. src/components/AddChoreDialog.tsx
- Update any gradient references

### 9. src/components/InviteDialog.tsx
- Update any gradient references

## Visual Design Changes Summary

| Element | Before (ChoreHub) | After (Chore Monkey) |
|---------|------------------|---------------------|
| Primary Color | Sage green | Warm brown |
| Accent Color | Orange | Banana yellow |
| Logo Icon | Home icon | Monkey emoji |
| Gradient | Sage tones | Brown/tan tones |
| Tone | Cozy, calm | Playful, energetic |
| Tagline | "Simple chore management" | "Make chores fun!" |

## Implementation Order
1. Update CSS variables in `src/index.css`
2. Update `tailwind.config.ts` gradient class
3. Update `index.html` meta tags
4. Update `Index.tsx` landing page
5. Update dashboard and other page components
6. Test all pages for visual consistency

## Technical Notes
- All color changes use HSL format for consistency with existing design system
- Dark mode colors will also be updated for proper contrast
- Gradient class renaming requires updating all component references
- No functionality changes - only visual/branding updates

