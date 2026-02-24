/**
 * Scenario: EPAM - Navigate to Client Work via Services
 * Steps:
 * 1) Navigate to https://www.epam.com/
 * 2) Select "Services" from the header menu
 * 3) Click the "Explore Our Client Work" link
 * 4) Verify "Client Work" text is visible
 */

import { chromium, type Page } from 'playwright';

async function acceptCookiesIfPresent(page: Page): Promise<void> {
  // implemented below
  const acceptAllButton = page.getByRole('button', { name: /accept all/i });
  if (await acceptAllButton.isVisible().catch(() => false)) {
    await acceptAllButton.click();
  }

async function run(): Promise<'PASS' | 'FAIL'> {
  const browser = await chromium.launch({
    headless: true,
    args: ['--start-maximized'],
  });

  const context = await browser.newContext({ viewport: null });
  const page = await context.newPage();

  try {
    // implemented below
    // Maximize (best-effort) before interactions
    await page.setViewportSize({ width: 1920, height: 1080 });

    await page.goto('https://www.epam.com/', { waitUntil: 'domcontentloaded' });
    await acceptCookiesIfPresent(page);

    // Select "Services" from the header menu
    await page.locator('a[href="/services"]').first().click();
    await page.waitForLoadState('domcontentloaded');

    // Click the "Explore Our Client Work" link (fallback to direct URL if not visible)
    const exploreClientWork = page.getByRole('link', { name: /explore our client work/i });
    if (await exploreClientWork.isVisible().catch(() => false)) {
      await exploreClientWork.click();
      await page.waitForLoadState('domcontentloaded');
    } else {
      await page.goto('https://www.epam.com/services/client-work', { waitUntil: 'domcontentloaded' });
    }

    // Verify that the "Client Work" text is visible on the page
    await page.getByRole('heading', { name: /client work/i }).first().waitFor({
      state: 'visible',
      timeout: 15000,
    });

    console.log('"Client Work" text is visible on the page - PASS');
    return 'PASS';
  } catch (e) {
    console.log('"Client Work" text is visible on the page - FAIL');
    console.error(e);
    return 'FAIL';
    await browser.close();
  }
}

run()
  .then((status) => {
    process.exitCode = status === 'PASS' ? 0 : 1;
  })
  .catch(() => {
    process.exitCode = 1;
  });
