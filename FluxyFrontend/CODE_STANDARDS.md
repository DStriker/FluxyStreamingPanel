# React-Dev Code Standards

This document defines the comprehensive code standards enforced by the react-dev plugin.

## Table of Contents

1. [React Standards](#react-standards)
2. [TypeScript Standards](#typescript-standards)
3. [Testing Standards](#testing-standards)
4. [Accessibility Standards](#accessibility-standards)
5. [Performance Standards](#performance-standards)
6. [Tailwind CSS Standards](#tailwind-css-standards)
7. [File Organization](#file-organization)
8. [Naming Conventions](#naming-conventions)

---

## React Standards

### Component Patterns

**✅ DO:**
```tsx
// Functional components only
interface ButtonProps {
  label: string;
  onClick: () => void;
  variant?: 'primary' | 'secondary';
}

export function Button({ label, onClick, variant = 'primary' }: ButtonProps) {
  return (
    <button onClick={onClick} className={`btn btn-${variant}`}>
      {label}
    </button>
  );
}
```

**❌ DON'T:**
```tsx
// No class components
class Button extends React.Component {
  render() {
    return <button>{this.props.label}</button>;
  }
}

// No default exports
export default Button;
```

### Hooks Rules

**✅ DO:**
```tsx
// Follow rules of hooks
function useUser(userId: string) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchUser(userId).then(setUser).finally(() => setLoading(false));
  }, [userId]);

  return { user, loading };
}

// Proper dependency arrays
useEffect(() => {
  // Effect logic
}, [dependency1, dependency2]);
```

**❌ DON'T:**
```tsx
// No conditional hooks
if (condition) {
  useEffect(() => { /* ... */ }, []);
}

// No hooks in loops
for (let i = 0; i < items.length; i++) {
  useState(items[i]);
}

// Missing dependencies
useEffect(() => {
  doSomething(dependency);
}, []); // dependency should be in array
```

### Component Composition

**✅ DO:**
```tsx
// Composition over inheritance
interface CardProps {
  children: React.ReactNode;
  header?: React.ReactNode;
  footer?: React.ReactNode;
}

export function Card({ children, header, footer }: CardProps) {
  return (
    <div className="card">
      {header && <div className="card-header">{header}</div>}
      <div className="card-body">{children}</div>
      {footer && <div className="card-footer">{footer}</div>}
    </div>
  );
}

// Use composition
<Card header={<h2>Title</h2>} footer={<Button label="Action" />}>
  <p>Content</p>
</Card>
```

**❌ DON'T:**
```tsx
// Don't use inheritance
class SpecialCard extends Card {
  // ...
}
```

### Error Boundaries

**✅ DO:**
```tsx
// Wrap features with error boundaries
export function FeatureErrorBoundary({ children }: { children: React.ReactNode }) {
  return (
    <ErrorBoundary
      fallback={<ErrorFallback />}
      onError={(error, errorInfo) => logError(error, errorInfo)}
    >
      {children}
    </ErrorBoundary>
  );
}
```

### Keys in Lists

**✅ DO:**
```tsx
// Use stable, unique keys
{items.map((item) => (
  <ListItem key={item.id} item={item} />
))}
```

**❌ DON'T:**
```tsx
// Don't use index as key
{items.map((item, index) => (
  <ListItem key={index} item={item} />
))}
```

---

## TypeScript Standards

### Strict Mode

**tsconfig.json must include:**
```json
{
  "compilerOptions": {
    "strict": true,
    "noImplicitAny": true,
    "strictNullChecks": true,
    "strictFunctionTypes": true,
    "strictBindCallApply": true,
    "strictPropertyInitialization": true,
    "noImplicitThis": true,
    "alwaysStrict": true
  }
}
```

### Type Safety

**✅ DO:**
```tsx
// Named exports for props interfaces
export interface UserProfileProps {
  userId: string;
  onUpdate: (user: User) => void;
}

// Use unknown instead of any
function processData(data: unknown) {
  if (isValidData(data)) {
    // Type guard narrows unknown to specific type
    return handleValidData(data);
  }
}

// Discriminated unions for variants
type ButtonVariant =
  | { variant: 'primary'; onClick: () => void }
  | { variant: 'link'; href: string }
  | { variant: 'disabled' };

// Utility types
type PartialUser = Partial<User>;
type UserWithoutPassword = Omit<User, 'password'>;
type UserCredentials = Pick<User, 'email' | 'password'>;
```

**❌ DON'T:**
```tsx
// No any types
function processData(data: any) { // ❌
  return data.whatever;
}

// No type assertions without validation
const user = data as User; // ❌ Unsafe
```

### Type Guards

**✅ DO:**
```tsx
// Implement type guards for runtime checking
function isUser(value: unknown): value is User {
  return (
    typeof value === 'object' &&
    value !== null &&
    'id' in value &&
    'email' in value
  );
}

// Use in code
if (isUser(data)) {
  console.log(data.email); // TypeScript knows data is User
}
```

---

## Testing Standards

### User-Centric Tests

**✅ DO:**
```tsx
// Test behavior, not implementation
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

describe('LoginForm', () => {
  it('allows user to log in with valid credentials', async () => {
    const user = userEvent.setup();
    const onLogin = vi.fn();

    render(<LoginForm onLogin={onLogin} />);

    // Find elements by role (accessibility-friendly)
    await user.type(screen.getByRole('textbox', { name: /email/i }), 'user@example.com');
    await user.type(screen.getByLabelText(/password/i), 'password123');
    await user.click(screen.getByRole('button', { name: /log in/i }));

    expect(onLogin).toHaveBeenCalledWith({
      email: 'user@example.com',
      password: 'password123'
    });
  });
});
```

**❌ DON'T:**
```tsx
// Don't test implementation details
expect(component.state.isLoading).toBe(true); // ❌
expect(mockFunction).toHaveBeenCalledTimes(1); // ❌ Usually not user-visible
```

### Query Priorities

**Priority order (from React Testing Library):**

1. **Accessible by everyone**: `getByRole`, `getByLabelText`, `getByPlaceholderText`, `getByText`
2. **Semantic queries**: `getByAltText`, `getByTitle`
3. **Test IDs** (last resort): `getByTestId`

**✅ DO:**
```tsx
// Prefer accessible queries
screen.getByRole('button', { name: /submit/i });
screen.getByLabelText(/email address/i);
screen.getByText(/welcome back/i);
```

**❌ DON'T:**
```tsx
// Avoid test IDs unless necessary
screen.getByTestId('submit-button'); // ❌ Use getByRole instead
```

### Accessibility Tests

**✅ DO:**
```tsx
import { axe, toHaveNoViolations } from 'jest-axe';

expect.extend(toHaveNoViolations);

it('should have no accessibility violations', async () => {
  const { container } = render(<Component />);
  const results = await axe(container);
  expect(results).toHaveNoViolations();
});
```

### Test Coverage Goals

- **Components**: 80%+ coverage
- **Hooks**: 100% coverage (critical business logic)
- **Services**: 90%+ coverage
- **Utilities**: 100% coverage

### Test Types

**Unit Tests** (Vitest + RTL):
```tsx
// src/features/user/components/UserCard.test.tsx
import { render, screen } from '@testing-library/react';
import { UserCard } from './UserCard';

describe('UserCard', () => {
  it('renders user information', () => {
    const user = { id: '1', name: 'John Doe', email: 'john@example.com' };
    render(<UserCard user={user} />);
    expect(screen.getByText('John Doe')).toBeInTheDocument();
  });
});
```

**Integration Tests** (RTL with context):
```tsx
// tests/integration/user/UserProfile.test.tsx
import { render, screen } from '@testing-library/react';
import { UserProvider } from '@/features/user/context/UserContext';
import { UserProfile } from '@/features/user/components/UserProfile';

it('loads and displays user profile', async () => {
  render(
    <UserProvider>
      <UserProfile userId="1" />
    </UserProvider>
  );
  expect(await screen.findByText('John Doe')).toBeInTheDocument();
});
```

**E2E Tests** (Playwright):
```ts
// tests/e2e/auth/login.spec.ts
import { test, expect } from '@playwright/test';

test('user can log in successfully', async ({ page }) => {
  await page.goto('/login');
  await page.fill('input[name="email"]', 'user@example.com');
  await page.fill('input[name="password"]', 'password123');
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL('/dashboard');
});
```

---

## Accessibility Standards

All components must meet **WCAG 2.1 Level AA** compliance.

### Semantic HTML

**✅ DO:**
```tsx
// Use semantic HTML elements
<header>
  <nav>
    <ul>
      <li><a href="/home">Home</a></li>
    </ul>
  </nav>
</header>

<main>
  <article>
    <h1>Article Title</h1>
    <section>
      <h2>Section Title</h2>
      <p>Content</p>
    </section>
  </article>
</main>

<footer>
  <p>&copy; 2024 Company</p>
</footer>
```

**❌ DON'T:**
```tsx
// Don't use divs for everything
<div className="header">
  <div className="nav">
    <div className="link">Home</div>
  </div>
</div>
```

### ARIA Usage

**✅ DO:**
```tsx
// Use ARIA only when HTML semantics are insufficient
<button aria-expanded={isOpen} aria-controls="menu">
  Menu
</button>
<div id="menu" role="menu" hidden={!isOpen}>
  <div role="menuitem">Item 1</div>
</div>

// Label interactive elements
<button aria-label="Close dialog">
  <X /> {/* Icon without text */}
</button>
```

**❌ DON'T:**
```tsx
// Don't use ARIA when HTML semantics exist
<div role="button" onClick={handleClick}> {/* ❌ Use <button> */}
  Click me
</div>
```

### Keyboard Navigation

**✅ DO:**
```tsx
// All interactive elements must be keyboard accessible
<button
  onClick={handleClick}
  onKeyDown={(e) => {
    if (e.key === 'Enter' || e.key === ' ') {
      handleClick();
    }
  }}
>
  Click me
</button>

// Proper focus management
useEffect(() => {
  if (isDialogOpen) {
    dialogRef.current?.focus();
  }
}, [isDialogOpen]);

// Focus trap in modals
<FocusTrap active={isModalOpen}>
  <Modal>{/* content */}</Modal>
</FocusTrap>
```

### Focus Indicators

**✅ DO:**
```css
/* Visible focus indicators */
button:focus-visible {
  outline: 2px solid blue;
  outline-offset: 2px;
}
```

**❌ DON'T:**
```css
/* Never remove focus without replacement */
button:focus {
  outline: none; /* ❌ */
}
```

### Color Contrast

**Requirements:**
- Normal text: 4.5:1 contrast ratio
- Large text (18pt+): 3:1 contrast ratio
- UI components: 3:1 contrast ratio

**✅ DO:**
```tsx
// High contrast text
<p className="text-gray-900 dark:text-gray-100">
  Content with sufficient contrast
</p>
```

### Screen Reader Support

**✅ DO:**
```tsx
// Descriptive labels
<input
  type="text"
  id="email"
  aria-label="Email address"
  aria-describedby="email-help"
/>
<span id="email-help">We'll never share your email</span>

// Live regions for dynamic content
<div role="status" aria-live="polite">
  {statusMessage}
</div>

// Skip links
<a href="#main-content" className="sr-only focus:not-sr-only">
  Skip to main content
</a>
```

---

## Performance Standards

### React.memo

**✅ DO:**
```tsx
// Memoize expensive components
export const ExpensiveComponent = React.memo(function ExpensiveComponent({
  data,
  onUpdate
}: ExpensiveComponentProps) {
  // Expensive rendering logic
  return <div>{/* ... */}</div>;
});

// Custom comparison
export const UserList = React.memo(
  function UserList({ users }: UserListProps) {
    return <ul>{users.map(user => <UserItem key={user.id} user={user} />)}</ul>;
  },
  (prevProps, nextProps) => {
    return prevProps.users.length === nextProps.users.length;
  }
);
```

### useMemo

**✅ DO:**
```tsx
// Memoize expensive computations
function DataTable({ data }: DataTableProps) {
  const sortedData = useMemo(() => {
    return [...data].sort((a, b) => a.name.localeCompare(b.name));
  }, [data]);

  return <table>{/* render sortedData */}</table>;
}
```

**❌ DON'T:**
```tsx
// Don't memoize cheap computations
const doubled = useMemo(() => count * 2, [count]); // ❌ Too simple
```

### useCallback

**✅ DO:**
```tsx
// Memoize callbacks passed to memoized children
function Parent() {
  const [count, setCount] = useState(0);

  const handleClick = useCallback(() => {
    setCount(c => c + 1);
  }, []);

  return <MemoizedChild onClick={handleClick} />;
}
```

### Code Splitting

**✅ DO:**
```tsx
// Lazy load routes
const Dashboard = lazy(() => import('./features/dashboard/components/Dashboard'));
const Settings = lazy(() => import('./features/settings/components/Settings'));

function App() {
  return (
    <Suspense fallback={<LoadingSpinner />}>
      <Routes>
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/settings" element={<Settings />} />
      </Routes>
    </Suspense>
  );
}

// Lazy load heavy components
const HeavyChart = lazy(() => import('./components/HeavyChart'));
```

### Bundle Size Monitoring

**Targets:**
- Initial bundle: <200KB gzipped
- Route chunks: <100KB gzipped
- Shared vendor chunk: <150KB gzipped

**Tools:**
```bash
# Analyze bundle
npm run build
npx vite-bundle-visualizer
```

### Virtual Scrolling

**✅ DO:**
```tsx
// Use virtual scrolling for large lists (>100 items)
import { useVirtualizer } from '@tanstack/react-virtual';

function VirtualList({ items }: VirtualListProps) {
  const parentRef = useRef<HTMLDivElement>(null);

  const virtualizer = useVirtualizer({
    count: items.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 50,
  });

  return (
    <div ref={parentRef} style={{ height: '500px', overflow: 'auto' }}>
      <div style={{ height: `${virtualizer.getTotalSize()}px` }}>
        {virtualizer.getVirtualItems().map((virtualItem) => (
          <div key={virtualItem.key} data-index={virtualItem.index}>
            {items[virtualItem.index].name}
          </div>
        ))}
      </div>
    </div>
  );
}
```

---

## Tailwind CSS Standards

### Utility-First Approach

**✅ DO:**
```tsx
// Use utility classes
<button className="px-4 py-2 bg-blue-500 text-white rounded hover:bg-blue-600 transition-colors">
  Click me
</button>

// Mobile-first responsive design
<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
  {items.map(item => <Card key={item.id} item={item} />)}
</div>

// Dark mode support
<div className="bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100">
  Content
</div>
```

**❌ DON'T:**
```tsx
// Don't write custom CSS unless absolutely necessary
<button style={{ padding: '8px 16px', backgroundColor: '#3b82f6' }}> {/* ❌ */}
  Click me
</button>
```

### Theme Extension

**✅ DO:**
```js
// tailwind.config.js
export default {
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#f0f9ff',
          100: '#e0f2fe',
          // ...
          900: '#0c4a6e',
        }
      },
      spacing: {
        '128': '32rem',
      }
    }
  }
}
```

### Component Extraction

**✅ DO:**
```tsx
// Extract to components when classes repeated >3 times
interface ButtonProps {
  variant?: 'primary' | 'secondary';
  children: React.ReactNode;
}

export function Button({ variant = 'primary', children }: ButtonProps) {
  const baseClasses = 'px-4 py-2 rounded font-medium transition-colors';
  const variantClasses = {
    primary: 'bg-blue-500 text-white hover:bg-blue-600',
    secondary: 'bg-gray-200 text-gray-900 hover:bg-gray-300'
  };

  return (
    <button className={`${baseClasses} ${variantClasses[variant]}`}>
      {children}
    </button>
  );
}
```

### Responsive Design

**✅ DO:**
```tsx
// Mobile-first breakpoints
<div className="
  text-sm md:text-base lg:text-lg     // Font size
  p-2 md:p-4 lg:p-6                   // Padding
  grid-cols-1 md:grid-cols-2 lg:grid-cols-3  // Layout
">
  Content
</div>
```

---

## File Organization

### Feature Structure

```
src/features/[feature-name]/
├── components/              # Feature components
│   ├── Component.tsx
│   ├── Component.test.tsx
│   └── Component.stories.tsx
├── hooks/                   # Feature hooks
│   ├── useFeature.ts
│   └── useFeature.test.ts
├── context/                 # Feature state
│   ├── FeatureContext.tsx
│   └── FeatureContext.test.tsx
├── services/                # API & business logic
│   ├── featureService.ts
│   └── featureService.test.ts
├── types/                   # Feature types
│   └── index.ts
├── utils/                   # Feature utilities
│   ├── helpers.ts
│   └── helpers.test.ts
└── index.ts                 # Public API (named exports)
```

### Public API Pattern

**✅ DO:**
```ts
// src/features/user/index.ts - Public API
export { UserProfile } from './components/UserProfile';
export { useUser } from './hooks/useUser';
export { UserProvider, useUserContext } from './context/UserContext';
export type { User, UserProfile as UserProfileType } from './types';

// Other features import from feature's public API
import { UserProfile, useUser } from '@/features/user';
```

**❌ DON'T:**
```ts
// Don't import from internal paths
import { UserProfile } from '@/features/user/components/UserProfile'; // ❌
```

---

## Naming Conventions

### Files

- **Components**: `PascalCase.tsx` (e.g., `UserProfile.tsx`)
- **Hooks**: `camelCase.ts` with `use` prefix (e.g., `useUser.ts`)
- **Services**: `camelCase.ts` with `Service` suffix (e.g., `userService.ts`)
- **Types**: `index.ts` in `types/` directory
- **Utils**: `camelCase.ts` (e.g., `formatDate.ts`)
- **Tests**: Same as source with `.test.ts(x)` suffix
- **Stories**: Same as component with `.stories.tsx` suffix

### Variables & Functions

```tsx
// Components: PascalCase
export function UserProfile() { }

// Hooks: camelCase with use prefix
export function useUser() { }

// Regular functions: camelCase
export function formatUserName() { }

// Constants: UPPER_SNAKE_CASE
export const MAX_USERS = 100;

// Interfaces: PascalCase with descriptive name
export interface UserProfileProps { }

// Types: PascalCase
export type UserRole = 'admin' | 'user';

// Boolean variables: is/has/should prefix
const isLoading = true;
const hasPermission = false;
const shouldRender = true;
```

### Event Handlers

```tsx
// Prefix with handle
const handleClick = () => { };
const handleSubmit = (e: FormEvent) => { };
const handleUserUpdate = (user: User) => { };

// Props: prefix with on
interface ButtonProps {
  onClick: () => void;
  onSubmit: (data: FormData) => void;
}
```

---

## Dependencies

### Required Dependencies

```json
{
  "dependencies": {
    "react": "^18.3.0",
    "react-dom": "^18.3.0"
  },
  "devDependencies": {
    "@types/react": "^18.3.0",
    "@types/react-dom": "^18.3.0",
    "@vitejs/plugin-react": "^4.3.0",
    "typescript": "^5.5.0",
    "vite": "^5.3.0",
    "vitest": "^2.0.0",
    "@testing-library/react": "^16.0.0",
    "@testing-library/user-event": "^14.5.0",
    "@testing-library/jest-dom": "^6.4.0",
    "@playwright/test": "^1.45.0",
    "tailwindcss": "^3.4.0",
    "postcss": "^8.4.0",
    "autoprefixer": "^10.4.0",
    "storybook": "^8.1.0",
    "@storybook/react-vite": "^8.1.0",
    "eslint": "^9.6.0",
    "prettier": "^3.3.0",
    "jest-axe": "^9.0.0"
  }
}
```

---

## Summary Checklist

Before committing code, ensure:

- [ ] Components are functional (no class components)
- [ ] TypeScript strict mode enabled, no `any` types
- [ ] Tests written (80%+ component coverage, 100% hook coverage)
- [ ] Accessibility tests included (axe-core)
- [ ] WCAG 2.1 AA compliance (semantic HTML, ARIA, keyboard nav)
- [ ] Performance optimizations (React.memo, useMemo, useCallback where needed)
- [ ] Tailwind utility classes used (no custom CSS unless necessary)
- [ ] Mobile-first responsive design
- [ ] Dark mode support
- [ ] Named exports used
- [ ] Props interfaces named and exported
- [ ] Error boundaries for features
- [ ] Proper keys in lists
- [ ] Files organized in feature directories
- [ ] Public API exposed via index.ts
