describe('Every protected API route rejects an unauthenticated caller', () => {
  it('checks the current controller source catalog without invoking business actions', () => {
    cy.clearAllCookies();
    cy.task('isolated:protectedRoutes', null, { log: false }).then(routes => {
      expect(routes, 'nonempty source-derived protected route surface').to.be.an('array').and.not.be.empty;
      expect(new Set(routes.map(r => r.controller)).size, 'all protected controllers including auth/me').to.be.at.least(31);
      cy.wrap(routes, { log: false }).each(route => {
        expect(route.path).to.match(/^\/api\/v1\//);
        cy.request({ method: route.method, url: route.path, failOnStatusCode: false,
          followRedirect: false, log: false }).then(response => {
          expect(response.status, `${route.controller}.${route.action} ${route.method} ${route.path}`).to.eq(401);
          // JwtBearer's fallback challenge can legitimately have an empty body.
          // The authentication contract is401 before model binding/action execution.
          if (response.body) expect(JSON.stringify(response.body), 'no infrastructure or credential disclosure')
            .not.to.match(/(?:at Microsoft\.|MySqlException|stackTrace|PasswordHash|SigningKey|Server=mysql)/i);
        });
      });
    });
  });
});
