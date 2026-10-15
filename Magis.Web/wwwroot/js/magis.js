(function () {
    'use strict';

    // Quita acentos y mayúsculas 
    function normalizar(texto) {
        return (texto || '').toString().normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
    }
    // Filtro en vivo de tablas
    document.querySelectorAll('[data-filtro-tabla]').forEach(function (campo) {
        var selector = campo.getAttribute('data-filtro-tabla');
        var tabla = document.querySelector(selector);
        if (!tabla || !tabla.tBodies.length) return;

        var contador = document.querySelector('[data-contador-tabla="' + selector + '"]');
        var filas = Array.prototype.slice.call(tabla.tBodies[0].rows);
        var textos = filas.map(function (fila) { return normalizar(fila.textContent); });

        function actualizar() {
            var termino = normalizar(campo.value).trim();
            var visibles = 0;

            filas.forEach(function (fila, i) {
                var coincide = termino === '' || textos[i].indexOf(termino) !== -1;
                fila.style.display = coincide ? '' : 'none';
                if (coincide) visibles++;
            });

            if (contador) {
                var total = filas.length;
                contador.textContent = termino === ''
                    ? total + (total === 1 ? ' registro' : ' registros')
                    : visibles + ' de ' + total + ' registros';
            }
        }

        campo.addEventListener('input', actualizar);
    });
    // Los avisos de éxito se cierran a los 7 segundos
    document.querySelectorAll('.alert.mg-auto-cerrar').forEach(function (aviso) {
        setTimeout(function () {
            if (window.bootstrap && document.body.contains(aviso)) {
                window.bootstrap.Alert.getOrCreateInstance(aviso).close();
            }
        }, 7000);
    });
    // Formularios
    document.addEventListener('submit', function (evento) {
        if (evento.defaultPrevented) return;
        var formulario = evento.target;
        if (!(formulario instanceof HTMLFormElement)) return;

        var mensaje = formulario.getAttribute('data-confirmar');
        if (mensaje && !window.confirm(mensaje)) {
            evento.preventDefault();
            return;
        }

        if (formulario.hasAttribute('data-bloquear')) {
            if (formulario.getAttribute('data-enviado') === '1') {
                evento.preventDefault();
                return;
            }
            formulario.setAttribute('data-enviado', '1');

            formulario.querySelectorAll('button[type="submit"]').forEach(function (boton) {
                boton.setAttribute('data-texto-original', boton.innerHTML);
                // Se deshabilita después de que el navegador toma los datos del formulario
                setTimeout(function () {
                    boton.disabled = true;
                    boton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Procesando...';
                }, 0);
            });
        }
    });

    // Si el usuario regresa con el botón "atrás", los formularios vuelven a quedar disponibles
    window.addEventListener('pageshow', function (evento) {
        if (!evento.persisted) return;
        document.querySelectorAll('form[data-enviado="1"]').forEach(function (formulario) {
            formulario.removeAttribute('data-enviado');
            formulario.querySelectorAll('button[type="submit"]').forEach(function (boton) {
                var original = boton.getAttribute('data-texto-original');
                if (original !== null) boton.innerHTML = original;
                boton.disabled = false;
            });
        });
    });
    // Botones de imprimir
    document.addEventListener('click', function (evento) {
        var boton = evento.target.closest ? evento.target.closest('[data-imprimir]') : null;
        if (boton) {
            evento.preventDefault();
            window.print();
        }
    });
})();
